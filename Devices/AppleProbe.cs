using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BebelEquipe155
{
    public sealed class AppleProbe
    {
        readonly ICommandRunner runner;
        public AppleProbe(ICommandRunner runner) { this.runner = runner; }

        public AppleSnapshot Probe(string ideviceInfoExe, string ideviceDiagnosticsExe, DeviceRef device)
        {
            if (device == null || device.Platform != DevicePlatform.Apple) throw new InvalidOperationException("iPhone/iPad inválido.");
            if (device.ConnectionState != DeviceConnectionState.Ready) throw new InvalidOperationException("iPhone/iPad indisponível.");
            string udid = DeviceTargeting.AppleUdid(device);
            string prefix = "-u \"" + udid + "\"";

            AppleSnapshot s = new AppleSnapshot();
            s.Device = device;
            ParseKeyValue(runner.Run(ideviceInfoExe, prefix, 15).StdOut, s.Info);
            ParseKeyValue(runner.Run(ideviceInfoExe, prefix + " -q com.apple.disk_usage", 15).StdOut, s.Info);
            ParseKeyValue(runner.Run(ideviceInfoExe, prefix + " -q com.apple.mobile.battery", 15).StdOut, s.Battery);

            long total, free;
            if (TryLong(s.Info, "TotalDiskCapacity", out total) || TryLong(s.Info, "TotalDataCapacity", out total)) s.StorageTotalBytes = total;
            if (TryLong(s.Info, "TotalDataAvailable", out free) || TryLong(s.Info, "AmountDataAvailable", out free)) s.StorageFreeBytes = free;
            if (s.StorageTotalBytes.HasValue && s.StorageFreeBytes.HasValue && s.StorageTotalBytes.Value >= s.StorageFreeBytes.Value)
                s.StorageUsedBytes = s.StorageTotalBytes.Value - s.StorageFreeBytes.Value;

            int pct;
            string pctText;
            if (s.Battery.TryGetValue("BatteryCurrentCapacity", out pctText) && int.TryParse(pctText, out pct)) s.BatteryPercent = pct;

            if (!string.IsNullOrWhiteSpace(ideviceDiagnosticsExe))
            {
                CommandResult diag = runner.Run(ideviceDiagnosticsExe, "-u \"" + udid + "\" ioregentry AppleSmartBattery", 30);
                string raw = diag.Combined;
                s.CycleCount = ParseXmlInteger(raw, "CycleCount");
                s.MaximumCapacityPercent = ParseXmlInteger(raw, "MaximumCapacityPercent");
                if (!s.MaximumCapacityPercent.HasValue) s.MaximumCapacityPercent = ParseXmlInteger(raw, "MaxCapacity");
            }
            return s;
        }

        public static void ParseKeyValue(string text, Dictionary<string,string> target)
        {
            if (target == null) return;
            foreach (string raw in (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int pos = raw.IndexOf(':');
                if (pos <= 0) continue;
                string key = raw.Substring(0, pos).Trim();
                string value = raw.Substring(pos + 1).Trim();
                if (key.Length > 0) target[key] = value;
            }
        }

        static bool TryLong(Dictionary<string,string> map, string key, out long value)
        {
            value = 0;
            string raw;
            return map != null && map.TryGetValue(key, out raw) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        static int? ParseXmlInteger(string text, string key)
        {
            Match m = Regex.Match(text ?? "", @"<key>" + Regex.Escape(key) + @"</key>\s*<integer>(\d+)</integer>", RegexOptions.IgnoreCase);
            int value;
            return m.Success && int.TryParse(m.Groups[1].Value, out value) ? (int?)value : null;
        }
    }
}
