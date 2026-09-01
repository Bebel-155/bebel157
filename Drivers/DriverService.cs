using System;
using System.Collections.Generic;
using System.Linq;

namespace BebelEquipe155
{
    public sealed class DriverService
    {
        readonly UsbDriverDiscovery discovery;
        readonly DriverResolver resolver;
        readonly DriverPackageManager packages;
        readonly DriverInstaller installer;
        readonly ICommandRunner runner;
        readonly string adbExe;

        public UsbDeviceSnapshot CurrentDevice { get; private set; }
        public DriverRecommendation CurrentRecommendation { get; private set; }

        public DriverService(UsbDriverDiscovery discovery, DriverResolver resolver, DriverPackageManager packages, DriverInstaller installer, ICommandRunner runner, string adbExe)
        {
            this.discovery = discovery;
            this.resolver = resolver;
            this.packages = packages;
            this.installer = installer;
            this.runner = runner;
            this.adbExe = adbExe;
        }

        static bool LooksAndroidUsb(UsbDeviceSnapshot d)
        {
            if (d == null) return false;
            string text = ((d.Manufacturer ?? "") + " " + (d.FriendlyName ?? "") + " " + (d.InstanceId ?? "")).ToLowerInvariant();
            return text.Contains("android") || text.Contains("adb") || text.Contains("samsung") || text.Contains("motorola") ||
                   text.Contains("xiaomi") || text.Contains("oneplus") || text.Contains("oppo") || text.Contains("realme") ||
                   text.Contains("vivo") || text.Contains("huawei") || text.Contains("honor") || text.Contains("sony") ||
                   text.Contains("asus") || text.Contains("nokia") || text.Contains("hmd") || text.Contains("nothing") ||
                   text.Contains("zte") || text.Contains("tcl") || text.Contains("alcatel") || text.Contains("lg") ||
                   !string.IsNullOrWhiteSpace(d.VendorId);
        }

        public DriverRecommendation Diagnose(string preferredAdbSerial)
        {
            List<UsbDeviceSnapshot> devices = discovery.DiscoverConnectedDevices().Where(LooksAndroidUsb).ToList();

            if (!string.IsNullOrWhiteSpace(adbExe))
            {
                CommandResult adb = runner.Run(adbExe, "devices -l", 10);
                UsbDriverDiscovery.AttachAdbState(devices, adb.Combined);
            }

            UsbDeviceSnapshot selected = null;
            if (!string.IsNullOrWhiteSpace(preferredAdbSerial))
                selected = devices.FirstOrDefault(x =>
                    string.Equals(x.AdbSerial, preferredAdbSerial, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(x.InstanceId) && x.InstanceId.IndexOf(preferredAdbSerial, StringComparison.OrdinalIgnoreCase) >= 0));

            if (selected == null && devices.Count == 1) selected = devices[0];
            if (selected == null && devices.Count > 1)
                selected = devices.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.AdbSerial)) ?? devices[0];

            CurrentDevice = selected;
            CurrentRecommendation = resolver.Resolve(selected, packages.GetAllPackages());
            return CurrentRecommendation;
        }

        public IEnumerable<DriverPackage> GetSelectableOfflinePackages()
        {
            return packages.GetInstallablePackages().OrderBy(x => x.Manufacturer).ThenBy(x => x.DisplayName);
        }

        public DriverInstallResult InstallRecommended(bool repair)
        {
            if (CurrentRecommendation == null)
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = "Execute o diagnóstico primeiro." };

            if (CurrentRecommendation.CurrentState == UsbAdbState.AdbReady)
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = "ADB já está funcionando; reinstalação não é necessária." };

            if (CurrentRecommendation.CurrentState == UsbAdbState.AdbUnauthorized)
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = "Autorize a depuração USB no aparelho antes de reinstalar driver." };

            if (CurrentRecommendation.CurrentState == UsbAdbState.AdbOffline && !repair)
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = "Reinicie o ADB e verifique cabo/porta antes de instalar driver." };

            DriverPackage package = packages.GetPackage(CurrentRecommendation.PackageId);
            if (package == null)
                return new DriverInstallResult { Success = false, ExitCode = -1, Message = "Nenhum pacote offline aprovado está disponível para este hardware." };

            DriverInstallResult result = installer.Install(package, repair);
            if (result.Success && !string.IsNullOrWhiteSpace(adbExe))
            {
                runner.Run(adbExe, "kill-server", 8);
                runner.Run(adbExe, "start-server", 8);
                Diagnose(CurrentDevice == null ? "" : CurrentDevice.AdbSerial);
            }
            return result;
        }
    }
}
