using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace BebelEquipe155
{
    public sealed class GithubReleaseAssetInfo
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public string Digest { get; set; }
    }

    public sealed class GithubReleaseInfo
    {
        public string TagName { get; set; }
        public string Body { get; set; }
        public GithubReleaseAssetInfo PortableExe { get; set; }
        public string Sha256AssetUrl { get; set; }
    }

    public static class GithubReleaseParser
    {
        public static GithubReleaseInfo Parse(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return null;
            JavaScriptSerializer json = new JavaScriptSerializer();
            json.MaxJsonLength = int.MaxValue;
            Dictionary<string,object> root = json.DeserializeObject(jsonText) as Dictionary<string,object>;
            if (root == null) return null;

            GithubReleaseInfo result = new GithubReleaseInfo();
            result.TagName = Text(root, "tag_name");
            result.Body = Text(root, "body");

            List<GithubReleaseAssetInfo> assets = new List<GithubReleaseAssetInfo>();
            object rawAssets;
            if (root.TryGetValue("assets", out rawAssets))
            {
                IEnumerable sequence = rawAssets as IEnumerable;
                if (sequence != null)
                {
                    foreach (object raw in sequence)
                    {
                        Dictionary<string,object> item = raw as Dictionary<string,object>;
                        if (item == null) continue;
                        GithubReleaseAssetInfo asset = new GithubReleaseAssetInfo();
                        asset.Name = Text(item, "name");
                        asset.DownloadUrl = Text(item, "browser_download_url");
                        asset.Digest = Text(item, "digest");
                        if (!string.IsNullOrWhiteSpace(asset.Name)) assets.Add(asset);
                    }
                }
            }

            foreach (GithubReleaseAssetInfo asset in assets)
            {
                if (!IsPortableBebelExe(asset.Name)) continue;
                if (string.IsNullOrWhiteSpace(asset.DownloadUrl)) continue;
                result.PortableExe = asset;
                break;
            }

            if (result.PortableExe != null)
            {
                string companion = result.PortableExe.Name + ".sha256";
                foreach (GithubReleaseAssetInfo asset in assets)
                {
                    if (string.Equals(asset.Name, companion, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(asset.DownloadUrl))
                    {
                        result.Sha256AssetUrl = asset.DownloadUrl;
                        break;
                    }
                }
            }
            return result;
        }

        static bool IsPortableBebelExe(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return name.StartsWith("Bebel-155_V", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("Bebel_Equipe_Do_Mais_Novo_155", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string Text(Dictionary<string,object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
        }
    }
}
