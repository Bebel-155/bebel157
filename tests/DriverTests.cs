using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BebelEquipe155;

class DriverFakeRunner : ICommandRunner
{
    readonly Dictionary<string, CommandResult> responses = new Dictionary<string, CommandResult>(StringComparer.OrdinalIgnoreCase);
    public readonly List<string> Calls = new List<string>();

    public void Add(string file, string args, string stdout, int exitCode)
    {
        responses[file + "|" + args] = new CommandResult { ExitCode = exitCode, StdOut = stdout, StdErr = "", TimedOut = false };
    }

    public void Add(string file, string args, string stdout)
    {
        Add(file, args, stdout, 0);
    }

    public CommandResult Run(string file, string arguments, int timeoutSeconds)
    {
        Calls.Add(file + "|" + arguments);
        CommandResult result;
        if (responses.TryGetValue(file + "|" + arguments, out result)) return result;
        return new CommandResult { ExitCode = 0, StdOut = "", StdErr = "", TimedOut = false };
    }
}

class DriverTests
{
    static int failures = 0;

    static void Main()
    {
        Run("DriverStateNamesAreStable", DriverStateNamesAreStable);
        Run("DiscoveryParsesSamsungVidPid", DiscoveryParsesSamsungVidPid);
        Run("ResolverDoesNotReinstallUnauthorized", ResolverDoesNotReinstallUnauthorized);
        Run("ResolverReadyNeedsNoDriver", ResolverReadyNeedsNoDriver);
        Run("ResolverMissingSelectsExactSamsung", ResolverMissingSelectsExactSamsung);
        Run("InstallerBuildsInfCommand", InstallerBuildsInfCommand);
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    static void DriverStateNamesAreStable()
    {
        TestAssert.Equal("DRIVER_MISSING", UsbAdbState.DriverMissing.ToWireName(), "missing");
        TestAssert.Equal("ADB_UNAUTHORIZED", UsbAdbState.AdbUnauthorized.ToWireName(), "unauthorized");
        TestAssert.Equal("ADB_READY", UsbAdbState.AdbReady.ToWireName(), "ready");
    }

    static void DiscoveryParsesSamsungVidPid()
    {
        DriverFakeRunner r = new DriverFakeRunner();
        UsbDriverDiscovery discovery = new UsbDriverDiscovery(r);
        string text =
            "Instance ID: USB\\VID_04E8&PID_6860\\R58M123456789\r\n" +
            "Device Description: SAMSUNG Mobile USB Composite Device\r\n" +
            "Manufacturer Name: SAMSUNG Electronics Co., Ltd.\r\n" +
            "Status: Started\r\n" +
            "Problem Code: 28\r\n\r\n";
        List<UsbDeviceSnapshot> list = discovery.ParseConnectedDevices(text);
        TestAssert.Equal(1, list.Count, "count");
        TestAssert.Equal("04E8", list[0].VendorId, "vid");
        TestAssert.Equal("6860", list[0].ProductId, "pid");
        TestAssert.Contains("SAMSUNG", list[0].Manufacturer, "manufacturer");
        TestAssert.Equal(28, list[0].ProblemCode.Value, "problem code");
    }

    static DriverPackage SamsungAllowed()
    {
        return new DriverPackage
        {
            Id = "samsung",
            Manufacturer = "Samsung",
            DisplayName = "Samsung USB Driver",
            RedistributionStatus = DriverRedistributionStatus.Allowed,
            UsbVendorIds = new[] { "04E8" },
            HardwareIdPatterns = new[] { "USB\\VID_04E8*" },
            Priority = 10
        };
    }

    static UsbDeviceSnapshot SamsungDevice(string adb, int? problem)
    {
        return new UsbDeviceSnapshot
        {
            InstanceId = "USB\\VID_04E8&PID_6860\\R58M",
            VendorId = "04E8",
            ProductId = "6860",
            Manufacturer = "SAMSUNG Electronics Co., Ltd.",
            HardwareIds = new[] { "USB\\VID_04E8&PID_6860" },
            ProblemCode = problem,
            Driver = new InstalledDriverInfo { Provider = "Samsung", InfName = "ssudbus.inf" },
            AdbStateRaw = adb
        };
    }

    static void ResolverDoesNotReinstallUnauthorized()
    {
        DriverRecommendation result = new DriverResolver().Resolve(
            SamsungDevice("unauthorized", null), new[] { SamsungAllowed() });
        TestAssert.Equal(UsbAdbState.AdbUnauthorized, result.CurrentState, "state");
        TestAssert.True(string.IsNullOrWhiteSpace(result.PackageId), "unauthorized must not recommend install");
        TestAssert.Contains("Autorize", result.Action, "action");
    }

    static void ResolverReadyNeedsNoDriver()
    {
        DriverRecommendation result = new DriverResolver().Resolve(
            SamsungDevice("device", null), new[] { SamsungAllowed() });
        TestAssert.Equal(UsbAdbState.AdbReady, result.CurrentState, "state");
        TestAssert.True(string.IsNullOrWhiteSpace(result.PackageId), "ready must not recommend package");
    }

    static void ResolverMissingSelectsExactSamsung()
    {
        UsbDeviceSnapshot d = SamsungDevice("", 28);
        d.Driver = null;
        DriverRecommendation result = new DriverResolver().Resolve(d, new[] { SamsungAllowed() });
        TestAssert.Equal(UsbAdbState.DriverMissing, result.CurrentState, "state");
        TestAssert.Equal("samsung", result.PackageId, "package");
        TestAssert.Equal(DriverConfidence.Exact, result.Confidence, "confidence");
    }

    static void InstallerBuildsInfCommand()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "BebelDriverTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            string inf = Path.Combine(tmp, "android_winusb.inf");
            File.WriteAllText(inf, "fixture");
            string lic = Path.Combine(tmp, "LICENSE.txt");
            File.WriteAllText(lic, "fixture");

            DriverPackageManager manager = new DriverPackageManager(tmp);
            DriverInstaller installer = new DriverInstaller(manager, new DriverFakeRunner());
            DriverPackage p = new DriverPackage
            {
                Id = "google",
                Manufacturer = "Google",
                DisplayName = "Google USB Driver",
                PackageType = DriverPackageType.Inf,
                RelativePath = "android_winusb.inf",
                RedistributionStatus = DriverRedistributionStatus.Allowed
            };

            DriverInstallCommand cmd = installer.BuildInstallCommand(p);
            TestAssert.Equal("pnputil.exe", cmd.FileName, "file");
            TestAssert.Contains("/add-driver", cmd.Arguments, "pnputil args");
            TestAssert.Contains("/install", cmd.Arguments, "install");
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }
}
