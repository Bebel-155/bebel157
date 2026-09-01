using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BebelEquipe155
{
    public enum DevicePlatform { Android, Apple }
    public enum DeviceConnectionState { Ready, Unauthorized, Offline, Unknown }
    public enum MatchConfidence { Exact, Probable, Incomplete }

    public sealed class DeviceRef
    {
        public DevicePlatform Platform { get; set; }
        public string TransportId { get; set; }
        public string TechnicalLabel { get; set; }
        public DeviceConnectionState ConnectionState { get; set; }

        public string MaskedTransportId
        {
            get
            {
                string x = TransportId ?? "";
                if (x.Length <= 8) return x;
                return x.Substring(0, 4) + "…" + x.Substring(x.Length - 4);
            }
        }

        public override string ToString()
        {
            string platform = Platform == DevicePlatform.Apple ? "iPhone/iPad" : "Android";
            string label = string.IsNullOrWhiteSpace(TechnicalLabel) ? "Dispositivo" : TechnicalLabel.Trim();
            return platform + " • " + label + " • " + MaskedTransportId;
        }
    }

    public sealed class AndroidSnapshot
    {
        public DeviceRef Device { get; set; }
        public Dictionary<string,string> Props { get; set; }
        public long RamTotalBytes { get; set; }
        public long StorageTotalBytes { get; set; }
        public long StorageUsedBytes { get; set; }
        public long StorageFreeBytes { get; set; }
        public int? BatteryPercent { get; set; }
        public string BatteryHealth { get; set; }
        public string BatteryStatus { get; set; }
        public string Resolution { get; set; }
        public string Density { get; set; }
        public string RawBattery { get; set; }
        public string RawStorage { get; set; }

        public AndroidSnapshot()
        {
            Props = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            RamTotalBytes = -1;
            StorageTotalBytes = -1;
            StorageUsedBytes = -1;
            StorageFreeBytes = -1;
        }
    }

    public sealed class AppleSnapshot
    {
        public DeviceRef Device { get; set; }
        public Dictionary<string,string> Info { get; set; }
        public Dictionary<string,string> Battery { get; set; }
        public long? StorageTotalBytes { get; set; }
        public long? StorageFreeBytes { get; set; }
        public long? StorageUsedBytes { get; set; }
        public int? BatteryPercent { get; set; }
        public int? CycleCount { get; set; }
        public int? MaximumCapacityPercent { get; set; }

        public AppleSnapshot()
        {
            Info = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            Battery = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class CatalogMatch
    {
        public bool Found { get; set; }
        public bool Exact { get; set; }
        public string CommercialName { get; set; }
        public string Manufacturer { get; set; }
        public string DeviceCode { get; set; }
        public long? RamBytes { get; set; }
        public List<string> StorageVariants { get; set; }
        public string CatalogVersion { get; set; }
        public List<string> Evidence { get; set; }

        public CatalogMatch()
        {
            StorageVariants = new List<string>();
            Evidence = new List<string>();
        }
    }

    public sealed class ResolvedDevice
    {
        public DeviceRef Device { get; set; }
        public string Manufacturer { get; set; }
        public string Brand { get; set; }
        public string CommercialName { get; set; }
        public string TechnicalModel { get; set; }
        public string DeviceCode { get; set; }
        public string Variant { get; set; }
        public string StorageVariant { get; set; }
        public string OperatingSystem { get; set; }
        public string Soc { get; set; }
        public string Abi { get; set; }
        public long? RamBytes { get; set; }
        public long? StorageTotalBytes { get; set; }
        public long? StorageUsedBytes { get; set; }
        public long? StorageFreeBytes { get; set; }
        public int? BatteryPercent { get; set; }
        public MatchConfidence Confidence { get; set; }
        public List<string> Evidence { get; set; }
        public string CatalogVersion { get; set; }

        public ResolvedDevice()
        {
            Evidence = new List<string>();
        }

        public string ConfidenceLabel
        {
            get
            {
                if (Confidence == MatchConfidence.Exact) return "EXATA";
                if (Confidence == MatchConfidence.Probable) return "PROVÁVEL";
                return "INCOMPLETA";
            }
        }
    }

    public static class DeviceTargeting
    {
        public static string SanitizeTransport(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Identificador do dispositivo ausente.");
            string clean = value.Replace("\"", "").Replace("\r", "").Replace("\n", "").Trim();
            if (clean.Length == 0) throw new InvalidOperationException("Identificador do dispositivo inválido.");
            return clean;
        }

        public static string AndroidPrefix(DeviceRef device)
        {
            if (device == null || device.Platform != DevicePlatform.Android)
                throw new InvalidOperationException("Selecione um Android.");
            return "-s \"" + SanitizeTransport(device.TransportId) + "\" ";
        }

        public static string AppleUdid(DeviceRef device)
        {
            if (device == null || device.Platform != DevicePlatform.Apple)
                throw new InvalidOperationException("Selecione um iPhone/iPad.");
            return SanitizeTransport(device.TransportId);
        }

        public static string FormatBytes(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value < 0) return "não disponível";
            double value = bytes.Value;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unit = 0;
            while (value >= 1024.0 && unit < units.Length - 1) { value /= 1024.0; unit++; }
            return value.ToString(value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00", CultureInfo.InvariantCulture) + " " + units[unit];
        }
    }
}
