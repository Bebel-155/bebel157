using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BebelEquipe155
{
    public sealed class UsbDriverDiscovery
    {
        readonly ICommandRunner runner;
        readonly string pnputilExe;

        public UsbDriverDiscovery(ICommandRunner runner) : this(runner, "pnputil.exe") { }

        public UsbDriverDiscovery(ICommandRunner runner, string pnputilExe)
        {
            this.runner = runner;
            this.pnputilExe = string.IsNullOrWhiteSpace(pnputilExe) ? "pnputil.exe" : pnputilExe;
        }

        static string NormalizeLabel(string label)
        {
            return (label ?? "").Trim().ToLowerInvariant()
                .Replace("á", "a").Replace("ã", "a").Replace("â", "a")
                .Replace("é", "e").Replace("ê", "e")
                .Replace("í", "i").Replace("ó", "o").Replace("õ", "o").Replace("ô", "o")
                .Replace("ú", "u").Replace("ç", "c");
        }

        static bool IsLabel(string label, params string[] options)
        {
            string x = NormalizeLabel(label);
            foreach (string option in options)
                if (x == NormalizeLabel(option)) return true;
            return false;
        }

        static string ExtractHex(string text, string key)
        {
            Match m = Regex.Match(text ?? "", key + "_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
        }

        static int? ParseProblemCode(string value)
        {
            int n;
            Match m = Regex.Match(value ?? "", @"-?\d+");
            if (m.Success && int.TryParse(m.Value, out n)) return n;
            return null;
        }

        public List<UsbDeviceSnapshot> DiscoverConnectedDevices()
        {
            CommandResult r = runner.Run(pnputilExe, "/enum-devices /connected /drivers", 15);
            if (r.ExitCode != 0 || string.IsNullOrWhiteSpace(r.StdOut))
                r = runner.Run(pnputilExe, "/enum-devices /connected", 15);

            return ParseConnectedDevices(r.Combined);
        }

        public List<UsbDeviceSnapshot> ParseConnectedDevices(string text)
        {
            List<UsbDeviceSnapshot> list = new List<UsbDeviceSnapshot>();
            UsbDeviceSnapshot current = null;
            List<string> hardwareIds = new List<string>();
            List<string> compatibleIds = new List<string>();
            bool readingHardware = false;
            bool readingCompatible = false;

            Action flush = delegate
            {
                if (current == null || string.IsNullOrWhiteSpace(current.InstanceId)) return;
                current.HardwareIds = hardwareIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                current.CompatibleIds = compatibleIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

                string evidence = current.InstanceId + " " + string.Join(" ", current.HardwareIds) + " " + string.Join(" ", current.CompatibleIds);
                current.VendorId = ExtractHex(evidence, "VID");
                current.ProductId = ExtractHex(evidence, "PID");
                list.Add(current);

                current = null;
                hardwareIds = new List<string>();
                compatibleIds = new List<string>();
                readingHardware = false;
                readingCompatible = false;
            };

            foreach (string raw in Regex.Split((text ?? "").Replace("\r\n", "\n"), "\n"))
            {
                string line = raw ?? "";
                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    flush();
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    string label = line.Substring(0, colon).Trim();
                    string value = line.Substring(colon + 1).Trim();

                    if (IsLabel(label, "Instance ID", "ID da Instância", "ID da Instancia"))
                    {
                        flush();
                        current = new UsbDeviceSnapshot();
                        current.InstanceId = value;
                        current.Driver = new InstalledDriverInfo();
                        readingHardware = readingCompatible = false;
                        continue;
                    }

                    if (current == null) continue;

                    readingHardware = IsLabel(label, "Hardware IDs", "IDs de Hardware");
                    readingCompatible = IsLabel(label, "Compatible IDs", "IDs Compatíveis", "IDs Compativeis");

                    if (IsLabel(label, "Device Description", "Descrição do Dispositivo", "Descricao do Dispositivo"))
                        current.FriendlyName = value;
                    else if (IsLabel(label, "Manufacturer Name", "Nome do Fabricante", "Manufacturer"))
                        current.Manufacturer = value;
                    else if (IsLabel(label, "Status"))
                        current.Status = value;
                    else if (IsLabel(label, "Problem Code", "Código do Problema", "Codigo do Problema"))
                        current.ProblemCode = ParseProblemCode(value);
                    else if (IsLabel(label, "Driver Name", "Nome do Driver"))
                        current.Driver.InfName = value;
                    else if (IsLabel(label, "Driver Provider", "Fornecedor do Driver", "Provider Name", "Nome do Provedor"))
                        current.Driver.Provider = value;
                    else if (IsLabel(label, "Driver Version", "Versão do Driver", "Versao do Driver"))
                        current.Driver.Version = value;
                    else if (readingHardware && value.Length > 0)
                        hardwareIds.Add(value);
                    else if (readingCompatible && value.Length > 0)
                        compatibleIds.Add(value);

                    continue;
                }

                if (current != null)
                {
                    if (readingHardware) hardwareIds.Add(trimmed);
                    else if (readingCompatible) compatibleIds.Add(trimmed);
                }
            }

            flush();
            return list;
        }

        public InstalledDriverInfo FindInstalledDriver(UsbDeviceSnapshot device)
        {
            if (device == null) return null;
            if (device.Driver != null && (!string.IsNullOrWhiteSpace(device.Driver.Provider) || !string.IsNullOrWhiteSpace(device.Driver.InfName)))
                return device.Driver;

            CommandResult r = runner.Run(pnputilExe, "/enum-drivers", 15);
            return ParseDriverStore(r.Combined, device.Driver == null ? "" : device.Driver.InfName);
        }

        public InstalledDriverInfo ParseDriverStore(string text, string preferredInf)
        {
            InstalledDriverInfo candidate = null;
            foreach (string block in Regex.Split((text ?? "").Replace("\r\n", "\n"), @"\n\s*\n"))
            {
                InstalledDriverInfo d = new InstalledDriverInfo();
                foreach (string raw in block.Split('\n'))
                {
                    int colon = raw.IndexOf(':');
                    if (colon <= 0) continue;
                    string label = raw.Substring(0, colon).Trim();
                    string value = raw.Substring(colon + 1).Trim();
                    if (IsLabel(label, "Published Name", "Nome Publicado")) d.InfName = value;
                    else if (IsLabel(label, "Provider Name", "Nome do Provedor", "Fornecedor do Driver")) d.Provider = value;
                    else if (IsLabel(label, "Driver Version", "Versão do Driver", "Versao do Driver")) d.Version = value;
                    else if (IsLabel(label, "Driver Date", "Data do Driver")) d.DriverDate = value;
                }
                if (string.IsNullOrWhiteSpace(d.InfName)) continue;
                if (!string.IsNullOrWhiteSpace(preferredInf) && string.Equals(d.InfName, preferredInf, StringComparison.OrdinalIgnoreCase))
                    return d;
                if (candidate == null) candidate = d;
            }
            return candidate;
        }

        public static void AttachAdbState(IEnumerable<UsbDeviceSnapshot> devices, string adbDevicesOutput)
        {
            if (devices == null) return;
            List<UsbDeviceSnapshot> list = devices.ToList();
            foreach (string raw in Regex.Split((adbDevicesOutput ?? "").Replace("\r\n", "\n"), "\n"))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) || line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts = Regex.Split(line, @"\s+");
                if (parts.Length < 2) continue;
                string serial = parts[0];
                string state = parts[1];

                UsbDeviceSnapshot match = list.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.InstanceId) &&
                    x.InstanceId.IndexOf(serial, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match == null && list.Count == 1) match = list[0];
                if (match == null) continue;

                match.AdbSerial = serial;
                match.AdbStateRaw = state;
            }
        }
    }
}
