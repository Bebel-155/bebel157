using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BebelEquipe155
{
    public sealed class DriverInstallCommand
    {
        public string FileName { get; set; }
        public string Arguments { get; set; }
        public bool RequiresElevation { get; set; }
    }

    public sealed class DriverInstaller
    {
        readonly DriverPackageManager packages;
        readonly ICommandRunner runner;

        public DriverInstaller(DriverPackageManager packages, ICommandRunner runner)
        {
            this.packages = packages;
            this.runner = runner;
        }

        public DriverInstallCommand BuildInstallCommand(DriverPackage package)
        {
            if (package == null) throw new ArgumentNullException("package");
            string path = packages.ResolveSafePath(package.RelativePath);
            string quoted = "\"" + path.Replace("\"", "") + "\"";

            if (package.PackageType == DriverPackageType.Inf)
            {
                return new DriverInstallCommand
                {
                    FileName = "pnputil.exe",
                    Arguments = "/add-driver " + quoted + " /install",
                    RequiresElevation = true
                };
            }

            if (package.PackageType == DriverPackageType.Msi)
            {
                return new DriverInstallCommand
                {
                    FileName = "msiexec.exe",
                    Arguments = "/i " + quoted + " " + (package.SilentInstallArguments ?? ""),
                    RequiresElevation = package.RequiresElevation
                };
            }

            return new DriverInstallCommand
            {
                FileName = path,
                Arguments = package.SilentInstallArguments ?? "",
                RequiresElevation = package.RequiresElevation
            };
        }

        static string EscapePowerShellSingleQuoted(string value)
        {
            return (value ?? "").Replace("'", "''");
        }

        public bool VerifyAuthenticode(DriverPackage package)
        {
            if (package == null || package.RedistributionStatus != DriverRedistributionStatus.Allowed) return false;
            string relative = string.IsNullOrWhiteSpace(package.SignatureRelativePath) ? package.RelativePath : package.SignatureRelativePath;
            string path = packages.ResolveSafePath(relative);
            if (!File.Exists(path)) return false;

            string ps = "$s=Get-AuthenticodeSignature -LiteralPath '" + EscapePowerShellSingleQuoted(path) + "';" +
                        "if($s.Status -ne 'Valid'){exit 2};" +
                        "$sub=$s.SignerCertificate.Subject;" +
                        "if($sub -notlike '*" + EscapePowerShellSingleQuoted(package.SignaturePublisher) + "*'){exit 3};exit 0";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(ps));
            string arg = "-NoProfile -NonInteractive -EncodedCommand " + encoded;
            CommandResult r = runner.Run("powershell.exe", arg, 20);
            return r.ExitCode == 0;
        }

        public DriverInstallResult Install(DriverPackage package, bool repair)
        {
            if (package == null) return new DriverInstallResult { Success = false, ExitCode = -1, Message = "Pacote não encontrado." };
            if (package.RedistributionStatus != DriverRedistributionStatus.Allowed)
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = "Pacote não aprovado para redistribuição offline." };
            if (!packages.VerifyHash(package))
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = "SHA-256 do pacote não confere." };
            if (!VerifyAuthenticode(package))
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = "Assinatura digital do pacote não foi validada." };

            DriverInstallCommand command = BuildInstallCommand(package);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = command.FileName;
                psi.Arguments = command.Arguments ?? "";
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Path.GetDirectoryName(packages.ResolveSafePath(package.RelativePath));
                if (command.RequiresElevation) psi.Verb = "runas";

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return new DriverInstallResult { Success = false, ExitCode = -1, Message = "Não foi possível iniciar o instalador." };
                    p.WaitForExit();
                    int code = p.ExitCode;
                    bool reboot = code == 3010 || code == 1641;
                    bool success = code == 0 || reboot;
                    return new DriverInstallResult
                    {
                        Success = success,
                        ExitCode = code,
                        RebootRequired = reboot,
                        Message = success ? (reboot ? "Driver instalado; reinicialização necessária." : "Driver instalado.") : "Instalador terminou com código " + code + "."
                    };
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                    return new DriverInstallResult { Success = false, ExitCode = 1223, UserCancelled = true, Message = "Elevação UAC cancelada pelo usuário." };
                return new DriverInstallResult { Success = false, ExitCode = ex.NativeErrorCode, Message = ex.Message };
            }
            catch (Exception ex)
            {
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = ex.Message };
            }
        }
    }
}
