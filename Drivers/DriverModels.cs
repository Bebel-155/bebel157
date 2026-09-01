using System;
using System.Collections.Generic;

namespace BebelEquipe155
{
    public enum UsbAdbState
    {
        DriverMissing,
        DriverIncorrect,
        AdbUnauthorized,
        AdbOffline,
        AdbReady,
        UsbOnly,
        Unknown
    }

    public static class UsbAdbStateExtensions
    {
        public static string ToWireName(this UsbAdbState state)
        {
            switch (state)
            {
                case UsbAdbState.DriverMissing: return "DRIVER_MISSING";
                case UsbAdbState.DriverIncorrect: return "DRIVER_INCORRECT";
                case UsbAdbState.AdbUnauthorized: return "ADB_UNAUTHORIZED";
                case UsbAdbState.AdbOffline: return "ADB_OFFLINE";
                case UsbAdbState.AdbReady: return "ADB_READY";
                case UsbAdbState.UsbOnly: return "USB_ONLY";
                default: return "UNKNOWN";
            }
        }
    }

    public enum DriverConfidence { Exact, Probable, Generic }
    public enum DriverPackageType { Inf, Exe, Msi }
    public enum DriverRedistributionStatus { Allowed, NotAllowed, Unknown }

    public sealed class InstalledDriverInfo
    {
        public string Provider { get; set; }
        public string Version { get; set; }
        public string DriverDate { get; set; }
        public string InfName { get; set; }
    }

    public sealed class UsbDeviceSnapshot
    {
        public string InstanceId { get; set; }
        public string[] HardwareIds { get; set; }
        public string[] CompatibleIds { get; set; }
        public string VendorId { get; set; }
        public string ProductId { get; set; }
        public string Manufacturer { get; set; }
        public string FriendlyName { get; set; }
        public string Status { get; set; }
        public int? ProblemCode { get; set; }
        public InstalledDriverInfo Driver { get; set; }
        public string AdbSerial { get; set; }
        public string AdbStateRaw { get; set; }

        public UsbDeviceSnapshot()
        {
            HardwareIds = new string[0];
            CompatibleIds = new string[0];
        }
    }

    public sealed class DriverPackage
    {
        public string Id { get; set; }
        public string Manufacturer { get; set; }
        public string DisplayName { get; set; }
        public string Version { get; set; }
        public DriverPackageType PackageType { get; set; }
        public string RelativePath { get; set; }
        public string SignatureRelativePath { get; set; }
        public string SilentInstallArguments { get; set; }
        public string SilentUninstallArguments { get; set; }
        public bool RequiresElevation { get; set; }
        public string[] SupportedArchitectures { get; set; }
        public string[] SupportedWindows { get; set; }
        public string[] UsbVendorIds { get; set; }
        public string[] HardwareIdPatterns { get; set; }
        public string Sha256 { get; set; }
        public string SignaturePublisher { get; set; }
        public string SourceUrl { get; set; }
        public DriverRedistributionStatus RedistributionStatus { get; set; }
        public string LicenseFile { get; set; }
        public int Priority { get; set; }
        public string FallbackPackageId { get; set; }

        public DriverPackage()
        {
            SupportedArchitectures = new string[0];
            SupportedWindows = new string[0];
            UsbVendorIds = new string[0];
            HardwareIdPatterns = new string[0];
        }
    }

    public sealed class DriverManifest
    {
        public int SchemaVersion { get; set; }
        public string GeneratedUtc { get; set; }
        public List<DriverPackage> Packages { get; set; }

        public DriverManifest()
        {
            Packages = new List<DriverPackage>();
        }
    }

    public sealed class DriverRecommendation
    {
        public UsbAdbState CurrentState { get; set; }
        public string PackageId { get; set; }
        public DriverConfidence Confidence { get; set; }
        public string Reason { get; set; }
        public string Action { get; set; }
    }

    public sealed class DriverInstallResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public bool RebootRequired { get; set; }
        public bool UserCancelled { get; set; }
        public string Message { get; set; }
    }
}
