using System;
using System.Collections.Generic;
using System.Linq;

namespace BebelEquipe155
{
    public sealed class CatalogManifest
    {
        public int schemaVersion { get; set; }
        public string generatedUtc { get; set; }
        public string androidVersion { get; set; }
        public string androidSha256 { get; set; }
        public string androidUrl { get; set; }
        public string appleVersion { get; set; }
        public string appleSha256 { get; set; }
        public string appleUrl { get; set; }
    }

    public sealed class AndroidCatalogEntry
    {
        public string manufacturer { get; set; }
        public string brand { get; set; }
        public string device { get; set; }
        public string modelCode { get; set; }
        public string commercialName { get; set; }
        public long? ramBytes { get; set; }
        public string soc { get; set; }
        public string gpu { get; set; }
        public string[] abis { get; set; }
        public string[] storageVariants { get; set; }
    }

    public sealed class AppleCatalogEntry
    {
        public string productType { get; set; }
        public string hardwareModel { get; set; }
        public string commercialName { get; set; }
        public string platform { get; set; }
        public string boardConfig { get; set; }
        public long? ramBytes { get; set; }
        public string[] storageVariants { get; set; }
    }

    public sealed class DeviceCatalog
    {
        public List<AndroidCatalogEntry> Android { get; set; }
        public List<AppleCatalogEntry> Apple { get; set; }
        public CatalogManifest Manifest { get; set; }
        public string Source { get; set; }

        public DeviceCatalog()
        {
            Android = new List<AndroidCatalogEntry>();
            Apple = new List<AppleCatalogEntry>();
        }
    }

    public static class CatalogKeys
    {
        static string N(string value) { return (value ?? "").Trim().ToLowerInvariant(); }
        public static string Android(string brand, string device, string modelCode)
        {
            return N(brand) + "|" + N(device) + "|" + N(modelCode);
        }
        public static string Apple(string productType, string hardwareModel)
        {
            return N(productType) + "|" + N(hardwareModel);
        }
    }

    public static class CatalogMatcher
    {
        static string Prop(AndroidSnapshot s, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value;
                if (s != null && s.Props != null && s.Props.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return "";
        }

        static string Info(AppleSnapshot s, string key)
        {
            string value;
            return s != null && s.Info != null && s.Info.TryGetValue(key, out value) ? (value ?? "").Trim() : "";
        }

        static bool Eq(string a, string b) { return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase); }

        public static CatalogMatch MatchAndroid(DeviceCatalog catalog, AndroidSnapshot snapshot, string version)
        {
            CatalogMatch none = new CatalogMatch { Found = false, CatalogVersion = version ?? "" };
            if (catalog == null || snapshot == null) return none;

            string brand = Prop(snapshot, "ro.product.brand", "ro.product.vendor.brand", "ro.product.product.brand");
            string manufacturer = Prop(snapshot, "ro.product.manufacturer", "ro.product.vendor.manufacturer", "ro.product.product.manufacturer");
            string device = Prop(snapshot, "ro.product.device", "ro.product.vendor.device", "ro.product.product.device", "ro.build.product");
            string model = Prop(snapshot, "ro.product.model", "ro.product.vendor.model", "ro.product.product.model", "ro.product.system.model");

            List<AndroidCatalogEntry> exact = catalog.Android.Where(e =>
                !string.IsNullOrWhiteSpace(device) && Eq(e.device, device) &&
                (Eq(e.brand, brand) || Eq(e.manufacturer, manufacturer) || Eq(e.brand, manufacturer)) &&
                !string.IsNullOrWhiteSpace(model) && Eq(e.modelCode, model)).ToList();

            AndroidCatalogEntry chosen = exact.FirstOrDefault();
            bool isExact = chosen != null;
            if (chosen == null)
            {
                List<AndroidCatalogEntry> byBrandDevice = catalog.Android.Where(e =>
                    !string.IsNullOrWhiteSpace(device) && Eq(e.device, device) &&
                    (Eq(e.brand, brand) || Eq(e.manufacturer, manufacturer) || Eq(e.brand, manufacturer))).ToList();

                if (byBrandDevice.Count > 0)
                {
                    List<string> names = byBrandDevice.Select(e => e.commercialName ?? "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    chosen = byBrandDevice.FirstOrDefault(e => Eq(e.modelCode, model)) ?? byBrandDevice[0];
                    isExact = names.Count == 1 && !string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(device);
                }
            }

            if (chosen == null && !string.IsNullOrWhiteSpace(model))
            {
                List<AndroidCatalogEntry> byModel = catalog.Android.Where(e => Eq(e.modelCode, model) &&
                    (string.IsNullOrWhiteSpace(manufacturer) || Eq(e.manufacturer, manufacturer) || Eq(e.brand, brand))).ToList();
                if (byModel.Count == 1) chosen = byModel[0];
                isExact = false;
            }

            if (chosen == null) return none;
            CatalogMatch m = new CatalogMatch();
            m.Found = true;
            m.Exact = isExact;
            m.CommercialName = chosen.commercialName;
            m.Manufacturer = chosen.manufacturer;
            m.DeviceCode = chosen.device;
            m.RamBytes = chosen.ramBytes;
            m.CatalogVersion = version ?? "";
            if (chosen.storageVariants != null) m.StorageVariants.AddRange(chosen.storageVariants);
            m.Evidence.Add("Catálogo Android: " + (version ?? "sem versão"));
            m.Evidence.Add("Chave: " + CatalogKeys.Android(string.IsNullOrWhiteSpace(chosen.brand) ? chosen.manufacturer : chosen.brand, chosen.device, chosen.modelCode));
            return m;
        }

        public static CatalogMatch MatchApple(DeviceCatalog catalog, AppleSnapshot snapshot, string version)
        {
            CatalogMatch none = new CatalogMatch { Found = false, CatalogVersion = version ?? "" };
            if (catalog == null || snapshot == null) return none;
            string productType = Info(snapshot, "ProductType");
            string hardware = Info(snapshot, "HardwareModel");
            AppleCatalogEntry exact = catalog.Apple.FirstOrDefault(e => Eq(e.productType, productType) && !string.IsNullOrWhiteSpace(hardware) && (Eq(e.hardwareModel, hardware) || Eq(e.boardConfig, hardware)));
            AppleCatalogEntry chosen = exact;
            bool isExact = exact != null;
            if (chosen == null)
            {
                List<AppleCatalogEntry> byProduct = catalog.Apple.Where(e => Eq(e.productType, productType)).ToList();
                if (byProduct.Count > 0)
                {
                    List<string> names = byProduct.Select(e => e.commercialName ?? "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    chosen = byProduct[0];
                    isExact = names.Count == 1 && string.IsNullOrWhiteSpace(hardware);
                }
            }
            if (chosen == null) return none;

            CatalogMatch m = new CatalogMatch();
            m.Found = true;
            m.Exact = isExact;
            m.CommercialName = chosen.commercialName;
            m.Manufacturer = "Apple";
            m.DeviceCode = string.IsNullOrWhiteSpace(chosen.hardwareModel) ? chosen.boardConfig : chosen.hardwareModel;
            m.RamBytes = chosen.ramBytes;
            m.CatalogVersion = version ?? "";
            if (chosen.storageVariants != null) m.StorageVariants.AddRange(chosen.storageVariants);
            m.Evidence.Add("Catálogo Apple: " + (version ?? "sem versão"));
            m.Evidence.Add("Chave: " + CatalogKeys.Apple(chosen.productType, m.DeviceCode));
            return m;
        }
    }
}
