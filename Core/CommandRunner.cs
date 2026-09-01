using System;
using System.Diagnostics;

namespace BebelEquipe155
{
    public sealed class CommandRunner : ICommandRunner
    {
        readonly Action<Process> onStart;
        readonly Action<Process> onExit;

        public CommandRunner() : this(null, null) { }

        public CommandRunner(Action<Process> onStart, Action<Process> onExit)
        {
            this.onStart = onStart;
            this.onExit = onExit;
        }

        public CommandResult Run(string file, string arguments, int timeoutSeconds)
        {
            if (string.IsNullOrWhiteSpace(file))
                return new CommandResult { ExitCode = -1, StdErr = "Executável não encontrado." };

            Process p = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = file;
                psi.Arguments = arguments ?? "";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                p = Process.Start(psi);
                if (p == null) return new CommandResult { ExitCode = -1, StdErr = "Não foi possível iniciar o processo." };
                if (onStart != null) onStart(p);

                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                bool exited = p.WaitForExit(Math.Max(1, timeoutSeconds) * 1000);
                if (!exited)
                {
                    try { p.Kill(); } catch { }
                    return new CommandResult { ExitCode = -2, StdOut = stdout, StdErr = stderr, TimedOut = true };
                }

                return new CommandResult { ExitCode = p.ExitCode, StdOut = stdout, StdErr = stderr, TimedOut = false };
            }
            catch (Exception ex)
            {
                return new CommandResult { ExitCode = -1, StdErr = ex.Message, TimedOut = false };
            }
            finally
            {
                try { if (onExit != null && p != null) onExit(p); } catch { }
                try { if (p != null) p.Dispose(); } catch { }
            }
        }
    }
}
