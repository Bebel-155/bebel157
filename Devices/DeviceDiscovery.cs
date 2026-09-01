using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BebelEquipe155
{
    public sealed class DeviceDiscovery
    {
        readonly ICommandRunner runner;
        public DeviceDiscovery(ICommandRunner runner) { this.runner = runner; }

        public List<DeviceRef> DiscoverAndroid(string adbExe)
        {
            List<DeviceRef> result = new List<DeviceRef>();
            if (string.IsNullOrWhiteSpace(adbExe)) return result;
            CommandResult r = runner.Run(adbExe, "devices -l", 12);
            string[] lines = (r.StdOut ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) || line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase)) continue;
                string[] tokens = Regex.Split(line, @"\s+");
                if (tokens.Length < 2) continue;
                DeviceConnectionState state = DeviceConnectionState.Unknown;
                if (tokens[1].Equals("device", StringComparison.OrdinalIgnoreCase)) state = DeviceConnectionState.Ready;
                else if (tokens[1].Equals("unauthorized", StringComparison.OrdinalIgnoreCase)) state = DeviceConnectionState.Unauthorized;
                else if (tokens[1].Equals("offline", StringComparison.OrdinalIgnoreCase)) state = DeviceConnectionState.Offline;
                string model = "";
                foreach (string t in tokens)
                    if (t.StartsWith("model:", StringComparison.OrdinalIgnoreCase)) model = t.Substring(6).Replace('_', ' ');
                result.Add(new DeviceRef { Platform = DevicePlatform.Android, TransportId = tokens[0], TechnicalLabel = model, ConnectionState = state });
            }
            return result;
        }

        public List<DeviceRef> DiscoverApple(string ideviceIdExe)
        {
            List<DeviceRef> result = new List<DeviceRef>();
            if (string.IsNullOrWhiteSpace(ideviceIdExe)) return result;
            CommandResult r = runner.Run(ideviceIdExe, "-l", 12);
            string[] lines = (r.StdOut ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in lines)
            {
                string udid = raw.Trim();
                if (udid.Length == 0) continue;
                result.Add(new DeviceRef { Platform = DevicePlatform.Apple, TransportId = udid, TechnicalLabel = "iPhone/iPad", ConnectionState = DeviceConnectionState.Ready });
            }
            return result;
        }
    }
}
