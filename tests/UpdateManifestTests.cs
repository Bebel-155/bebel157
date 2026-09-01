using System;
using BebelEquipe155;

class UpdateManifestTests
{
    static int failures;
    static void Main()
    {
        Run("NestedGithubAssetsAreParsed", NestedGithubAssetsAreParsed);
        Run("SetupIsNotSelectedAsPortableExe", SetupIsNotSelectedAsPortableExe);
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    static void NestedGithubAssetsAreParsed()
    {
        string json = "{\"tag_name\":\"v5.2.0\",\"body\":\"notes\",\"assets\":[" +
            "{\"name\":\"Bebel-155_V5_2_0.exe\",\"browser_download_url\":\"https://example/app.exe\",\"digest\":\"sha256:abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd\",\"uploader\":{\"login\":\"bot\"}}," +
            "{\"name\":\"Bebel-155_V5_2_0.exe.sha256\",\"browser_download_url\":\"https://example/app.exe.sha256\"}," +
            "{\"name\":\"Bebel-155_Setup_V5_2_0.exe\",\"browser_download_url\":\"https://example/setup.exe\"}]}";

        GithubReleaseInfo r = GithubReleaseParser.Parse(json);
        TestAssert.Equal("v5.2.0", r.TagName, "tag");
        TestAssert.Equal("Bebel-155_V5_2_0.exe", r.PortableExe.Name, "portable exe");
        TestAssert.Equal("https://example/app.exe.sha256", r.Sha256AssetUrl, "sha asset");
        TestAssert.True(r.PortableExe.Digest.StartsWith("sha256:"), "digest");
    }

    static void SetupIsNotSelectedAsPortableExe()
    {
        string json = "{\"tag_name\":\"v5.2.0\",\"assets\":[" +
            "{\"name\":\"Bebel-155_Setup_V5_2_0.exe\",\"browser_download_url\":\"https://example/setup.exe\"}," +
            "{\"name\":\"Bebel-155_V5_2_0.exe\",\"browser_download_url\":\"https://example/app.exe\"}]}";
        GithubReleaseInfo r = GithubReleaseParser.Parse(json);
        TestAssert.Equal("Bebel-155_V5_2_0.exe", r.PortableExe.Name, "must select portable exe");
    }
}
