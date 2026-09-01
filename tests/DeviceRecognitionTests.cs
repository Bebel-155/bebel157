using System;
using System.Collections.Generic;
using System.Linq;
using BebelEquipe155;

class FakeRunner : ICommandRunner
{
    readonly Dictionary<string, CommandResult> responses = new Dictionary<string, CommandResult>(StringComparer.OrdinalIgnoreCase);
    public readonly List<string> Calls = new List<string>();

    public void Add(string file, string args, string stdout)
    {
        responses[file + "|" + args] = new CommandResult { ExitCode = 0, StdOut = stdout, StdErr = "", TimedOut = false };
    }

    public CommandResult Run(string file, string arguments, int timeoutSeconds)
    {
        Calls.Add(file + "|" + arguments);
        CommandResult result;
        if (responses.TryGetValue(file + "|" + arguments, out result)) return result;
        return new CommandResult { ExitCode = 0, StdOut = "", StdErr = "", TimedOut = false };
    }
}

class DeviceRecognitionTests
{
    static int failures = 0;

    static void Main()
    {
        Run("DeviceRefMasksIdentifier", DeviceRefMasksIdentifier);
        Run("CommandResultCarriesExitAndText", CommandResultCarriesExitAndText);
        Run("DiscoveryParsesTwoAndroids", DiscoveryParsesTwoAndroids);
        Run("DiscoveryParsesAppleUdids", DiscoveryParsesAppleUdids);
        Run("AndroidProbeTargetsSerialAndParsesMetrics", AndroidProbeTargetsSerialAndParsesMetrics);
        Run("AppleProbeTargetsUdidAndParsesMetrics", AppleProbeTargetsUdidAndParsesMetrics);
        Run("ResolverExactAndroid", ResolverExactAndroid);
        Run("ResolverUnknownAppleDoesNotGuess", ResolverUnknownAppleDoesNotGuess);
        Run("TargetingBuildsSerialPrefix", TargetingBuildsSerialPrefix);
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    static void DeviceRefMasksIdentifier()
    {
        DeviceRef d = new DeviceRef { Platform = DevicePlatform.Android, TransportId = "R58M123456789", TechnicalLabel = "SM-S921B", ConnectionState = DeviceConnectionState.Ready };
        TestAssert.Equal("R58M…6789", d.MaskedTransportId, "masked transport");
    }

    static void CommandResultCarriesExitAndText()
    {
        CommandResult r = new CommandResult { ExitCode = 7, StdOut = "out", StdErr = "err", TimedOut = false };
        TestAssert.Equal(7, r.ExitCode, "exit");
        TestAssert.Equal("out", r.StdOut, "stdout");
        TestAssert.Equal("err", r.StdErr, "stderr");
    }

    static void DiscoveryParsesTwoAndroids()
    {
        FakeRunner f = new FakeRunner();
        f.Add("adb.exe", "devices -l", "List of devices attached\r\nR58A\tdevice product:e1 model:SM-S921B device:e1 transport_id:1\r\nABC2\tunauthorized usb:1-2 transport_id:2\r\n");
        DeviceDiscovery d = new DeviceDiscovery(f);
        List<DeviceRef> list = d.DiscoverAndroid("adb.exe");
        TestAssert.Equal(2, list.Count, "android count");
        TestAssert.Equal("R58A", list[0].TransportId, "serial");
        TestAssert.Equal(DeviceConnectionState.Ready, list[0].ConnectionState, "state1");
        TestAssert.Equal(DeviceConnectionState.Unauthorized, list[1].ConnectionState, "state2");
    }

    static void DiscoveryParsesAppleUdids()
    {
        FakeRunner f = new FakeRunner();
        f.Add("idevice_id.exe", "-l", "00008110ABCDEF\r\n00008120FEDCBA\r\n");
        DeviceDiscovery d = new DeviceDiscovery(f);
        List<DeviceRef> list = d.DiscoverApple("idevice_id.exe");
        TestAssert.Equal(2, list.Count, "apple count");
    }

    static void AndroidProbeTargetsSerialAndParsesMetrics()
    {
        FakeRunner f = new FakeRunner();
        string p = "-s \"R58A\" ";
        f.Add("adb.exe", p + "shell getprop", "[ro.product.brand]: [samsung]\r\n[ro.product.manufacturer]: [samsung]\r\n[ro.product.model]: [SM-S921B]\r\n[ro.product.device]: [e1]\r\n[ro.product.marketname]: [Galaxy S24]\r\n[ro.build.version.release]: [16]\r\n[ro.build.version.sdk]: [36]\r\n[ro.soc.model]: [Exynos 2400]\r\n");
        f.Add("adb.exe", p + "shell cat /proc/meminfo", "MemTotal:        7782400 kB\r\n");
        f.Add("adb.exe", p + "shell df -k /data", "Filesystem 1K-blocks Used Available Use% Mounted on\r\n/dev/block/dm-53 124000000 64000000 60000000 52% /data\r\n");
        f.Add("adb.exe", p + "shell dumpsys battery", "level: 83\r\nhealth: 2\r\n");
        f.Add("adb.exe", p + "shell wm size", "Physical size: 1080x2340\r\n");
        f.Add("adb.exe", p + "shell wm density", "Physical density: 420\r\n");

        DeviceRef device = new DeviceRef { Platform = DevicePlatform.Android, TransportId = "R58A", ConnectionState = DeviceConnectionState.Ready };
        AndroidSnapshot s = new AndroidProbe(f).Probe("adb.exe", device);
        TestAssert.Equal("SM-S921B", s.Props["ro.product.model"], "model");
        TestAssert.Equal(7782400L * 1024L, s.RamTotalBytes, "ram");
        TestAssert.Equal(124000000L * 1024L, s.StorageTotalBytes, "storage total");
        TestAssert.Equal(83, s.BatteryPercent.Value, "battery");
        foreach (string call in f.Calls.Where(x => x.StartsWith("adb.exe|", StringComparison.OrdinalIgnoreCase)))
            TestAssert.True(call.StartsWith("adb.exe|-s \"R58A\" ", StringComparison.OrdinalIgnoreCase), "untargeted adb: " + call);
    }

    static void AppleProbeTargetsUdidAndParsesMetrics()
    {
        FakeRunner f = new FakeRunner();
        string u = "-u \"00008110ABCDEF\"";
        f.Add("ideviceinfo.exe", u, "ProductType: iPhone16,1\r\nHardwareModel: D83AP\r\nProductVersion: 26.6.1\r\nBuildVersion: 23G93\r\nModelNumber: MTQ63BR/A\r\nDeviceClass: iPhone\r\n");
        f.Add("ideviceinfo.exe", u + " -q com.apple.disk_usage", "TotalDiskCapacity: 255852544000\r\nTotalDataAvailable: 101000000000\r\n");
        f.Add("ideviceinfo.exe", u + " -q com.apple.mobile.battery", "BatteryCurrentCapacity: 78\r\nBatteryIsCharging: false\r\n");
        DeviceRef device = new DeviceRef { Platform = DevicePlatform.Apple, TransportId = "00008110ABCDEF", ConnectionState = DeviceConnectionState.Ready };
        AppleSnapshot s = new AppleProbe(f).Probe("ideviceinfo.exe", null, device);
        TestAssert.Equal("iPhone16,1", s.Info["ProductType"], "product type");
        TestAssert.Equal(255852544000L, s.StorageTotalBytes.Value, "disk");
        TestAssert.Equal(78, s.BatteryPercent.Value, "battery");
        foreach (string call in f.Calls.Where(x => x.StartsWith("ideviceinfo.exe|", StringComparison.OrdinalIgnoreCase)))
            TestAssert.Contains(u, call, "untargeted ideviceinfo");
    }

    static void ResolverExactAndroid()
    {
        AndroidSnapshot s = new AndroidSnapshot {
            Device = new DeviceRef { Platform = DevicePlatform.Android, TransportId = "X", ConnectionState = DeviceConnectionState.Ready },
            Props = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
                { "ro.product.manufacturer", "Samsung" }, { "ro.product.brand", "Samsung" }, { "ro.product.model", "SM-S921B" }, { "ro.product.device", "e1" }, { "ro.build.version.release", "16" }
            }, RamTotalBytes = 8000000000L
        };
        CatalogMatch m = new CatalogMatch { Found = true, Exact = true, CommercialName = "Galaxy S24", Manufacturer = "Samsung", DeviceCode = "e1", CatalogVersion = "2026-09-01", Evidence = new List<string>() };
        ResolvedDevice r = new DeviceResolver().ResolveAndroid(s, m);
        TestAssert.Equal(MatchConfidence.Exact, r.Confidence, "confidence");
        TestAssert.Equal("Galaxy S24", r.CommercialName, "commercial");
        TestAssert.Equal("SM-S921B", r.TechnicalModel, "technical");
    }

    static void ResolverUnknownAppleDoesNotGuess()
    {
        AppleSnapshot s = new AppleSnapshot {
            Device = new DeviceRef { Platform = DevicePlatform.Apple, TransportId = "U", ConnectionState = DeviceConnectionState.Ready },
            Info = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { { "ProductType", "iPhone16,99" }, { "HardwareModel", "UNKNOWNAP" }, { "ProductVersion", "26.6.1" } },
            Battery = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        };
        ResolvedDevice r = new DeviceResolver().ResolveApple(s, new CatalogMatch { Found = false, Evidence = new List<string>() });
        TestAssert.Equal(MatchConfidence.Incomplete, r.Confidence, "confidence");
        TestAssert.Equal("iPhone16,99", r.CommercialName, "no guessed name");
    }

    static void TargetingBuildsSerialPrefix()
    {
        DeviceRef d = new DeviceRef { Platform = DevicePlatform.Android, TransportId = "R58A", ConnectionState = DeviceConnectionState.Ready };
        TestAssert.Equal("-s \"R58A\" ", DeviceTargeting.AndroidPrefix(d), "prefix");
    }
}
