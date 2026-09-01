using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace BebelEquipe155
{
    public interface ICatalogTextClient
    {
        string DownloadString(string url);
    }

    public sealed class HttpCatalogTextClient : ICatalogTextClient
    {
        public string DownloadString(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 12000;
            request.ReadWriteTimeout = 12000;
            request.UserAgent = "BebelEquipe155/5.2";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }
    }

    public sealed class CatalogUpdateResult
    {
        public bool Success { get; set; }
        public bool Changed { get; set; }
        public string Message { get; set; }
        public CatalogManifest Manifest { get; set; }
    }

    public sealed class CatalogManager
    {
        readonly string seedDir;
        readonly string cacheDir;
        readonly ICatalogTextClient client;
        readonly JavaScriptSerializer json = new JavaScriptSerializer();
        readonly object sync = new object();
        DeviceCatalog current;

        public CatalogManager(string seedDir, string cacheDir) : this(seedDir, cacheDir, new HttpCatalogTextClient()) { }

        public CatalogManager(string seedDir, string cacheDir, ICatalogTextClient client)
        {
            this.seedDir = seedDir;
            this.cacheDir = cacheDir;
            this.client = client;
            json.MaxJsonLength = int.MaxValue;
            Directory.CreateDirectory(cacheDir);
        }

        public DeviceCatalog Current
        {
            get { lock (sync) { return current; } }
        }

        public DeviceCatalog LoadBestAvailable()
        {
            DeviceCatalog loaded = TryLoadDirectory(cacheDir, "cache local");
            if (loaded == null) loaded = TryLoadDirectory(seedDir, "catálogo embutido");
            if (loaded == null) loaded = new DeviceCatalog { Source = "sem catálogo" };
            lock (sync) current = loaded;
            return loaded;
        }

        DeviceCatalog TryLoadDirectory(string dir, string source)
        {
            try
            {
                string androidPath = Path.Combine(dir, "android_devices.json");
                string applePath = Path.Combine(dir, "apple_devices.json");
                string manifestPath = Path.Combine(dir, "catalog-manifest.json");
                if (!File.Exists(androidPath) || !File.Exists(applePath)) return null;
                string aText = File.ReadAllText(androidPath, Encoding.UTF8);
                string pText = File.ReadAllText(applePath, Encoding.UTF8);
                List<AndroidCatalogEntry> a = json.Deserialize<List<AndroidCatalogEntry>>(aText);
                List<AppleCatalogEntry> p = json.Deserialize<List<AppleCatalogEntry>>(pText);
                if (a == null || p == null) return null;
                CatalogManifest manifest = null;
                if (File.Exists(manifestPath))
                {
                    manifest = json.Deserialize<CatalogManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
                    if (manifest != null)
                    {
                        if (!string.IsNullOrWhiteSpace(manifest.androidSha256) && !HashEquals(aText, manifest.androidSha256)) return null;
                        if (!string.IsNullOrWhiteSpace(manifest.appleSha256) && !HashEquals(pText, manifest.appleSha256)) return null;
                    }
                }
                return new DeviceCatalog { Android = a, Apple = p, Manifest = manifest, Source = source };
            }
            catch { return null; }
        }

        public CatalogUpdateResult Refresh(string manifestUrl)
        {
            try
            {
                string manifestText = client.DownloadString(manifestUrl);
                CatalogManifest manifest = json.Deserialize<CatalogManifest>(manifestText);
                if (manifest == null || manifest.schemaVersion != 1) return Fail("Manifesto de catálogo inválido.");
                string baseUrl = manifestUrl.Substring(0, manifestUrl.LastIndexOf('/') + 1);
                string androidUrl = string.IsNullOrWhiteSpace(manifest.androidUrl) ? baseUrl + "android_devices.json" : manifest.androidUrl;
                string appleUrl = string.IsNullOrWhiteSpace(manifest.appleUrl) ? baseUrl + "apple_devices.json" : manifest.appleUrl;

                bool changed = NeedsUpdate(manifest);
                if (!changed)
                {
                    DeviceCatalog unchanged = LoadBestAvailable();
                    return new CatalogUpdateResult { Success = true, Changed = false, Message = "Catálogos já estão atualizados.", Manifest = unchanged.Manifest ?? manifest };
                }

                string androidText = client.DownloadString(androidUrl);
                string appleText = client.DownloadString(appleUrl);
                List<AndroidCatalogEntry> android = json.Deserialize<List<AndroidCatalogEntry>>(androidText);
                List<AppleCatalogEntry> apple = json.Deserialize<List<AppleCatalogEntry>>(appleText);
                if (android == null || android.Count == 0) return Fail("Catálogo Android recebido está vazio.");
                if (apple == null || apple.Count == 0) return Fail("Catálogo Apple recebido está vazio.");
                if (!HashEquals(androidText, manifest.androidSha256)) return Fail("SHA-256 do catálogo Android não confere.");
                if (!HashEquals(appleText, manifest.appleSha256)) return Fail("SHA-256 do catálogo Apple não confere.");

                Directory.CreateDirectory(cacheDir);
                AtomicWrite(Path.Combine(cacheDir, "android_devices.json"), androidText);
                AtomicWrite(Path.Combine(cacheDir, "apple_devices.json"), appleText);
                AtomicWrite(Path.Combine(cacheDir, "catalog-manifest.json"), manifestText);

                DeviceCatalog loaded = LoadBestAvailable();
                if (loaded.Android.Count == 0 || loaded.Apple.Count == 0) return Fail("Catálogo baixado não pôde ser recarregado.");
                return new CatalogUpdateResult { Success = true, Changed = true, Message = "Catálogos atualizados com sucesso.", Manifest = loaded.Manifest };
            }
            catch (Exception ex)
            {
                return Fail("Falha ao atualizar catálogos: " + ex.Message);
            }
        }

        bool NeedsUpdate(CatalogManifest remote)
        {
            DeviceCatalog local = Current ?? LoadBestAvailable();
            if (local == null || local.Manifest == null) return true;
            return !string.Equals(local.Manifest.androidSha256 ?? "", remote.androidSha256 ?? "", StringComparison.OrdinalIgnoreCase) ||
                   !string.Equals(local.Manifest.appleSha256 ?? "", remote.appleSha256 ?? "", StringComparison.OrdinalIgnoreCase);
        }

        public CatalogMatch MatchAndroid(AndroidSnapshot snapshot)
        {
            DeviceCatalog c = Current ?? LoadBestAvailable();
            string version = c.Manifest == null ? "" : c.Manifest.androidVersion;
            return CatalogMatcher.MatchAndroid(c, snapshot, version);
        }

        public CatalogMatch MatchApple(AppleSnapshot snapshot)
        {
            DeviceCatalog c = Current ?? LoadBestAvailable();
            string version = c.Manifest == null ? "" : c.Manifest.appleVersion;
            return CatalogMatcher.MatchApple(c, snapshot, version);
        }

        CatalogUpdateResult Fail(string message)
        {
            return new CatalogUpdateResult { Success = false, Changed = false, Message = message, Manifest = Current == null ? null : Current.Manifest };
        }

        static bool HashEquals(string text, string expected)
        {
            if (string.IsNullOrWhiteSpace(expected)) return false;
            return string.Equals(Sha256Utf8(text), expected.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static string Sha256Utf8(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(text ?? ""));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static void AtomicWrite(string path, string content)
        {
            string temp = path + ".download";
            string backup = path + ".bak";
            File.WriteAllText(temp, content ?? "", new UTF8Encoding(false));
            if (File.Exists(backup)) File.Delete(backup);
            try
            {
                if (File.Exists(path)) File.Move(path, backup);
                File.Move(temp, path);
                if (File.Exists(backup)) File.Delete(backup);
            }
            catch
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                try { if (File.Exists(backup)) File.Move(backup, path); } catch { }
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                throw;
            }
        }
    }
}
