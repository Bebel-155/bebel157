using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace BebelEquipe155
{
    public sealed class DriverPackageManager
    {
        readonly string driversRoot;
        DriverManifest manifest;
        readonly Dictionary<string, DriverPackage> byId = new Dictionary<string, DriverPackage>(StringComparer.OrdinalIgnoreCase);

        public DriverPackageManager(string driversRoot)
        {
            this.driversRoot = Path.GetFullPath(driversRoot ?? "");
        }

        static string S(IDictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : "";
        }

        static bool B(IDictionary<string, object> d, string key)
        {
            object v;
            bool b;
            if (d != null && d.TryGetValue(key, out v) && v != null && bool.TryParse(Convert.ToString(v), out b)) return b;
            return false;
        }

        static int I(IDictionary<string, object> d, string key, int fallback)
        {
            object v;
            int n;
            if (d != null && d.TryGetValue(key, out v) && v != null && int.TryParse(Convert.ToString(v), out n)) return n;
            return fallback;
        }

        static string[] A(IDictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return new string[0];
            ArrayList al = v as ArrayList;
            if (al != null) return al.Cast<object>().Select(x => Convert.ToString(x) ?? "").ToArray();
            object[] oa = v as object[];
            if (oa != null) return oa.Select(x => Convert.ToString(x) ?? "").ToArray();
            IEnumerable enumerable = v as IEnumerable;
            if (enumerable != null && !(v is string))
            {
                List<string> result = new List<string>();
                foreach (object item in enumerable) result.Add(Convert.ToString(item) ?? "");
                return result.ToArray();
            }
            return new[] { Convert.ToString(v) ?? "" };
        }

        static DriverPackageType ParsePackageType(string value)
        {
            DriverPackageType result;
            return Enum.TryParse<DriverPackageType>(value ?? "", true, out result) ? result : DriverPackageType.Exe;
        }

        static DriverRedistributionStatus ParseRedistribution(string value)
        {
            DriverRedistributionStatus result;
            return Enum.TryParse<DriverRedistributionStatus>(value ?? "", true, out result) ? result : DriverRedistributionStatus.Unknown;
        }

        static DriverPackage MapPackage(IDictionary<string, object> d)
        {
            return new DriverPackage
            {
                Id = S(d, "Id"),
                Manufacturer = S(d, "Manufacturer"),
                DisplayName = S(d, "DisplayName"),
                Version = S(d, "Version"),
                PackageType = ParsePackageType(S(d, "PackageType")),
                RelativePath = S(d, "RelativePath"),
                SignatureRelativePath = S(d, "SignatureRelativePath"),
                SilentInstallArguments = S(d, "SilentInstallArguments"),
                SilentUninstallArguments = S(d, "SilentUninstallArguments"),
                RequiresElevation = B(d, "RequiresElevation"),
                SupportedArchitectures = A(d, "SupportedArchitectures"),
                SupportedWindows = A(d, "SupportedWindows"),
                UsbVendorIds = A(d, "UsbVendorIds"),
                HardwareIdPatterns = A(d, "HardwareIdPatterns"),
                Sha256 = S(d, "Sha256"),
                SignaturePublisher = S(d, "SignaturePublisher"),
                SourceUrl = S(d, "SourceUrl"),
                RedistributionStatus = ParseRedistribution(S(d, "RedistributionStatus")),
                LicenseFile = S(d, "LicenseFile"),
                Priority = I(d, "Priority", 1000),
                FallbackPackageId = S(d, "FallbackPackageId")
            };
        }

        public DriverManifest LoadManifest(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            object rootObj = serializer.DeserializeObject(json);
            IDictionary<string, object> root = rootObj as IDictionary<string, object>;
            if (root == null) throw new InvalidDataException("Manifesto de drivers inválido.");

            DriverManifest parsed = new DriverManifest();
            parsed.SchemaVersion = I(root, "SchemaVersion", 0);
            parsed.GeneratedUtc = S(root, "GeneratedUtc");
            if (parsed.SchemaVersion != 1) throw new InvalidDataException("Manifesto de drivers incompatível.");

            object packagesObj;
            if (root.TryGetValue("Packages", out packagesObj) && packagesObj != null)
            {
                IEnumerable enumerable = packagesObj as IEnumerable;
                if (enumerable != null)
                {
                    foreach (object item in enumerable)
                    {
                        IDictionary<string, object> d = item as IDictionary<string, object>;
                        if (d != null) parsed.Packages.Add(MapPackage(d));
                    }
                }
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DriverPackage package in parsed.Packages)
            {
                ValidateMetadata(package);
                if (!ids.Add(package.Id)) throw new InvalidDataException("ID de driver duplicado: " + package.Id);
            }

            manifest = parsed;
            byId.Clear();
            foreach (DriverPackage package in parsed.Packages) byId[package.Id] = package;
            return manifest;
        }

        void ValidateMetadata(DriverPackage package)
        {
            if (package == null) throw new InvalidDataException("Entrada de driver nula.");
            if (string.IsNullOrWhiteSpace(package.Id)) throw new InvalidDataException("Driver sem Id.");
            if (string.IsNullOrWhiteSpace(package.Manufacturer)) throw new InvalidDataException("Driver sem fabricante: " + package.Id);

            if (package.RedistributionStatus == DriverRedistributionStatus.Allowed)
            {
                if (string.IsNullOrWhiteSpace(package.RelativePath)) throw new InvalidDataException("Pacote permitido sem arquivo: " + package.Id);
                if (!Regex.IsMatch(package.Sha256 ?? "", "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("SHA-256 inválido: " + package.Id);
                if (string.IsNullOrWhiteSpace(package.SourceUrl)) throw new InvalidDataException("Origem ausente: " + package.Id);
                if (string.IsNullOrWhiteSpace(package.LicenseFile)) throw new InvalidDataException("Licença ausente: " + package.Id);
                if (string.IsNullOrWhiteSpace(package.SignaturePublisher)) throw new InvalidDataException("Publisher ausente: " + package.Id);
                ResolveSafePath(package.RelativePath);
                ResolveSafePath(package.LicenseFile);
                if (!string.IsNullOrWhiteSpace(package.SignatureRelativePath)) ResolveSafePath(package.SignatureRelativePath);
            }
        }

        public string ResolveSafePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return "";
            string full = Path.GetFullPath(Path.Combine(driversRoot, relativePath));
            string prefix = driversRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, driversRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Caminho fora da pasta Drivers: " + relativePath);
            return full;
        }

        public DriverPackage GetPackage(string id)
        {
            DriverPackage package;
            return !string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id, out package) ? package : null;
        }

        public IEnumerable<DriverPackage> GetAllPackages()
        {
            return manifest == null || manifest.Packages == null ? Enumerable.Empty<DriverPackage>() : manifest.Packages;
        }

        public IEnumerable<DriverPackage> GetInstallablePackages()
        {
            return GetAllPackages().Where(x => x.RedistributionStatus == DriverRedistributionStatus.Allowed);
        }

        public bool VerifyHash(DriverPackage package)
        {
            if (package == null || package.RedistributionStatus != DriverRedistributionStatus.Allowed) return false;
            string path = ResolveSafePath(package.RelativePath);
            if (!File.Exists(path)) return false;

            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                return string.Equals(actual, (package.Sha256 ?? "").Trim().ToLowerInvariant(), StringComparison.Ordinal);
            }
        }
    }
}
