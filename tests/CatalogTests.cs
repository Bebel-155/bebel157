using System;
using System.Collections.Generic;
using System.IO;
using BebelEquipe155;

class FakeCatalogClient : ICatalogTextClient
{
    public readonly Dictionary<string,string> Data = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    public string DownloadString(string url) { return Data[url]; }
}

class CatalogTests
{
    static int failures;
    static void Main()
    {
        Run("CatalogKeysNormalize", CatalogKeysNormalize);
        Run("ExactAndroidMatch", ExactAndroidMatch);
        Run("UnknownAppleDoesNotMatch", UnknownAppleDoesNotMatch);
        Run("BadHashPreservesCache", BadHashPreservesCache);
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    static void CatalogKeysNormalize()
    {
        TestAssert.Equal("samsung|e1|sm-s921b", CatalogKeys.Android("Samsung", "e1", "SM-S921B"), "android key");
        TestAssert.Equal("iphone16,1|d83ap", CatalogKeys.Apple("iPhone16,1", "D83AP"), "apple key");
    }

    static void ExactAndroidMatch()
    {
        DeviceCatalog c = new DeviceCatalog();
        c.Android.Add(new AndroidCatalogEntry { manufacturer="Samsung", brand="Samsung", device="e1", modelCode="SM-S921B", commercialName="Galaxy S24" });
        AndroidSnapshot s = new AndroidSnapshot();
        s.Props["ro.product.manufacturer"]="Samsung"; s.Props["ro.product.brand"]="Samsung"; s.Props["ro.product.device"]="e1"; s.Props["ro.product.model"]="SM-S921B";
        CatalogMatch m = CatalogMatcher.MatchAndroid(c, s, "test");
        TestAssert.True(m.Found && m.Exact, "exact android");
        TestAssert.Equal("Galaxy S24", m.CommercialName, "commercial");
    }

    static void UnknownAppleDoesNotMatch()
    {
        DeviceCatalog c = new DeviceCatalog();
        c.Apple.Add(new AppleCatalogEntry { productType="iPhone16,1", hardwareModel="D83AP", commercialName="iPhone 15 Pro" });
        AppleSnapshot s = new AppleSnapshot(); s.Info["ProductType"]="iPhone99,9"; s.Info["HardwareModel"]="X";
        CatalogMatch m = CatalogMatcher.MatchApple(c, s, "test");
        TestAssert.True(!m.Found, "unknown must not match");
    }

    static void BadHashPreservesCache()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "B155CatalogTest_" + Guid.NewGuid().ToString("N"));
        string seed = Path.Combine(baseDir, "seed"); string cache = Path.Combine(baseDir, "cache");
        Directory.CreateDirectory(seed); Directory.CreateDirectory(cache);
        string old = "[{\"manufacturer\":\"Samsung\",\"brand\":\"Samsung\",\"device\":\"e1\",\"modelCode\":\"SM-S921B\",\"commercialName\":\"Galaxy S24\"}]";
        File.WriteAllText(Path.Combine(cache, "android_devices.json"), old);
        File.WriteAllText(Path.Combine(cache, "apple_devices.json"), "[]");
        FakeCatalogClient f = new FakeCatalogClient();
        f.Data["https://example/manifest.json"] = "{\"schemaVersion\":1,\"androidVersion\":\"new\",\"androidSha256\":\"deadbeef\",\"appleVersion\":\"new\",\"appleSha256\":\"deadbeef\",\"androidUrl\":\"https://example/android.json\",\"appleUrl\":\"https://example/apple.json\"}";
        f.Data["https://example/android.json"] = "[]"; f.Data["https://example/apple.json"] = "[]";
        CatalogManager m = new CatalogManager(seed, cache, f);
        CatalogUpdateResult r = m.Refresh("https://example/manifest.json");
        TestAssert.True(!r.Success, "bad hash must fail");
        TestAssert.Equal(old, File.ReadAllText(Path.Combine(cache, "android_devices.json")), "cache preserved");
        try { Directory.Delete(baseDir, true); } catch { }
    }
}
