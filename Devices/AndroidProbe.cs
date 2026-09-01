using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BebelEquipe155
{
    public sealed class AndroidProbe
    {
        readonly ICommandRunner runner;
        public AndroidProbe(ICommandRunner runner) { this.runner = runner; }

        CommandResult Adb(string adbExe, DeviceRef device, string args, int timeout)
        {
            return runner.Run(adbExe, DeviceTargeting.AndroidPrefix(device) + args, timeout);
        }

        public AndroidSnapshot Probe(string adbExe, DeviceRef device)
        {
            if (device == null || device.Platform != DevicePlatform.Android) throw new InvalidOperationException("Android inválido.");
            if (device.ConnectionState != DeviceConnectionState.Ready) throw new InvalidOperationException("Android não autorizado ou offline.");

            AndroidSnapshot s = new AndroidSnapshot();
            s.Device = device;
            ParseGetProp(Adb(adbExe, device, "shell getprop", 15).StdOut, s.Props);

            string mem = Adb(adbExe, device, "shell cat /proc/meminfo", 15).StdOut ?? "";
            Match mm = Regex.Match(mem, @"(?im)^MemTotal:\s+(\d+)\s+kB");
            long kbytes;
            if (mm.Success && long.TryParse(mm.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out kbytes))
                s.RamTotalBytes = kbytes * 1024L;

            string df = Adb(adbExe, device, "shell df -k /data", 20).StdOut ?? "";
            s.RawStorage = df.Trim();
            ParseDfData(df, s);

            string battery = Adb(adbExe, device, "shell dumpsys battery", 15).StdOut ?? "";
            s.RawBattery = battery.Trim();
            s.BatteryPercent = ParseIntField(battery, "level");
            s.BatteryHealth = ParseStringField(battery, "health");
            s.BatteryStatus = ParseStringField(battery, "status");

            s.Resolution = ParseScreenValue(Adb(adbExe, device, "shell wm size", 12).StdOut, "size");
            s.Density = ParseScreenValue(Adb(adbExe, device, "shell wm density", 12).StdOut, "density");
            return s;
        }

        public static void ParseGetProp(string text, Dictionary<string,string> props)
        {
            if (props == null) return;
            string[] lines = (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Regex rx = new Regex(@"^\[([^]]+)\]: \[(.*)\]$");
            foreach (string line in lines)
            {
                Match m = rx.Match(line.Trim());
                if (m.Success) props[m.Groups[1].Value] = m.Groups[2].Value;
            }
        }

        static void ParseDfData(string text, AndroidSnapshot s)
        {
            string selected = null;
            foreach (string raw in (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.EndsWith(" /data", StringComparison.OrdinalIgnoreCase) || line.EndsWith(" /data/", StringComparison.OrdinalIgnoreCase)) selected = line;
            }
            if (selected == null) return;
            string[] t = Regex.Split(selected, @"\s+");
            if (t.Length < 5) return;
            long total, used, free;
            if (long.TryParse(t[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out total) &&
                long.TryParse(t[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out used) &&
                long.TryParse(t[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out free))
            {
                s.StorageTotalBytes = total * 1024L;
                s.StorageUsedBytes = used * 1024L;
                s.StorageFreeBytes = free * 1024L;
            }
        }

        static int? ParseIntField(string text, string name)
        {
            Match m = Regex.Match(text ?? "", @"(?im)^\s*" + Regex.Escape(name) + @":\s*(-?\d+)\s*$");
            int value;
            return m.Success && int.TryParse(m.Groups[1].Value, out value) ? (int?)value : null;
        }

        static string ParseStringField(string text, string name)
        {
            Match m = Regex.Match(text ?? "", @"(?im)^\s*" + Regex.Escape(name) + @":\s*(.*?)\s*$");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        static string ParseScreenValue(string text, string kind)
        {
            Match physical = Regex.Match(text ?? "", @"(?im)^Physical\s+" + Regex.Escape(kind) + @":\s*(.+)$");
            if (physical.Success) return physical.Groups[1].Value.Trim();
            Match over = Regex.Match(text ?? "", @"(?im)^Override\s+" + Regex.Escape(kind) + @":\s*(.+)$");
            return over.Success ? over.Groups[1].Value.Trim() : "";
        }
    }
}
