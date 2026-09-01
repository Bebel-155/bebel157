using System;
using System.Collections.Generic;
using System.Linq;

namespace BebelEquipe155
{
    public sealed class DeviceResolver
    {
        public ResolvedDevice ResolveAndroid(AndroidSnapshot s, CatalogMatch match)
        {
            if (s == null) throw new ArgumentNullException("s");
            if (match == null) match = new CatalogMatch();
            ResolvedDevice r = new ResolvedDevice();
            r.Device = s.Device;
            r.Manufacturer = Prop(s, "ro.product.manufacturer", "ro.product.vendor.manufacturer", "ro.product.product.manufacturer", "ro.product.system.manufacturer");
            r.Brand = Prop(s, "ro.product.brand", "ro.product.vendor.brand", "ro.product.product.brand");
            r.TechnicalModel = Prop(s, "ro.product.model", "ro.product.vendor.model", "ro.product.product.model", "ro.product.system.model");
            r.DeviceCode = Prop(s, "ro.product.device", "ro.product.vendor.device", "ro.product.product.device", "ro.build.product");
            string market = Prop(s, "ro.product.marketname", "ro.product.vendor.marketname", "ro.product.product.marketname");
            r.OperatingSystem = "Android " + Prop(s, "ro.build.version.release");
            string sdk = Prop(s, "ro.build.version.sdk");
            if (!string.IsNullOrWhiteSpace(sdk)) r.OperatingSystem += " • SDK " + sdk;
            r.Soc = Prop(s, "ro.soc.model", "ro.hardware", "ro.board.platform");
            r.Abi = Prop(s, "ro.product.cpu.abilist", "ro.product.cpu.abi");
            r.RamBytes = s.RamTotalBytes >= 0 ? (long?)s.RamTotalBytes : null;
            r.StorageTotalBytes = s.StorageTotalBytes >= 0 ? (long?)s.StorageTotalBytes : null;
            r.StorageUsedBytes = s.StorageUsedBytes >= 0 ? (long?)s.StorageUsedBytes : null;
            r.StorageFreeBytes = s.StorageFreeBytes >= 0 ? (long?)s.StorageFreeBytes : null;
            r.BatteryPercent = s.BatteryPercent;

            AddEvidence(r, "Fabricante", r.Manufacturer);
            AddEvidence(r, "Marca", r.Brand);
            AddEvidence(r, "Modelo técnico", r.TechnicalModel);
            AddEvidence(r, "Codinome", r.DeviceCode);
            AddEvidence(r, "Market name do aparelho", market);

            bool conflict = HasAndroidIdentityConflict(s);
            if (match.Found)
            {
                r.CommercialName = string.IsNullOrWhiteSpace(match.CommercialName) ? (string.IsNullOrWhiteSpace(market) ? r.TechnicalModel : market) : match.CommercialName;
                if (string.IsNullOrWhiteSpace(r.Manufacturer)) r.Manufacturer = match.Manufacturer;
                r.CatalogVersion = match.CatalogVersion;
                if (match.RamBytes.HasValue && !r.RamBytes.HasValue) r.RamBytes = match.RamBytes;
                if (match.StorageVariants != null && match.StorageVariants.Count == 1) r.StorageVariant = match.StorageVariants[0];
                if (match.Evidence != null) r.Evidence.AddRange(match.Evidence);
                r.Confidence = match.Exact && !conflict ? MatchConfidence.Exact : MatchConfidence.Probable;
                if (conflict && !match.Exact) r.Confidence = MatchConfidence.Incomplete;
            }
            else
            {
                r.CommercialName = string.IsNullOrWhiteSpace(market) ? r.TechnicalModel : market;
                r.Confidence = conflict ? MatchConfidence.Incomplete : MatchConfidence.Probable;
                if (string.IsNullOrWhiteSpace(r.TechnicalModel) || string.IsNullOrWhiteSpace(r.DeviceCode)) r.Confidence = MatchConfidence.Incomplete;
            }
            return r;
        }

        public ResolvedDevice ResolveApple(AppleSnapshot s, CatalogMatch match)
        {
            if (s == null) throw new ArgumentNullException("s");
            if (match == null) match = new CatalogMatch();
            ResolvedDevice r = new ResolvedDevice();
            r.Device = s.Device;
            r.Manufacturer = "Apple";
            r.Brand = "Apple";
            string productType = Info(s, "ProductType");
            string hardware = Info(s, "HardwareModel");
            r.TechnicalModel = productType;
            r.DeviceCode = hardware;
            r.Variant = Info(s, "ModelNumber");
            r.OperatingSystem = "iOS " + Info(s, "ProductVersion");
            r.StorageTotalBytes = s.StorageTotalBytes;
            r.StorageFreeBytes = s.StorageFreeBytes;
            r.StorageUsedBytes = s.StorageUsedBytes;
            r.BatteryPercent = s.BatteryPercent;
            AddEvidence(r, "ProductType", productType);
            AddEvidence(r, "HardwareModel", hardware);
            AddEvidence(r, "ModelNumber", r.Variant);

            if (match.Found)
            {
                r.CommercialName = string.IsNullOrWhiteSpace(match.CommercialName) ? productType : match.CommercialName;
                r.CatalogVersion = match.CatalogVersion;
                r.RamBytes = match.RamBytes;
                if (match.StorageVariants != null && match.StorageVariants.Count == 1) r.StorageVariant = match.StorageVariants[0];
                if (match.Evidence != null) r.Evidence.AddRange(match.Evidence);
                r.Confidence = match.Exact ? MatchConfidence.Exact : MatchConfidence.Probable;
            }
            else
            {
                r.CommercialName = productType;
                r.Confidence = MatchConfidence.Incomplete;
            }
            if (string.IsNullOrWhiteSpace(productType)) r.Confidence = MatchConfidence.Incomplete;
            return r;
        }

        static string Prop(AndroidSnapshot s, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value;
                if (s.Props != null && s.Props.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return "";
        }

        static string Info(AppleSnapshot s, string key)
        {
            string value;
            return s.Info != null && s.Info.TryGetValue(key, out value) ? (value ?? "").Trim() : "";
        }

        static bool HasAndroidIdentityConflict(AndroidSnapshot s)
        {
            string[] modelKeys = { "ro.product.model", "ro.product.vendor.model", "ro.product.product.model", "ro.product.system.model" };
            List<string> models = new List<string>();
            foreach (string key in modelKeys)
            {
                string v;
                if (s.Props != null && s.Props.TryGetValue(key, out v) && !string.IsNullOrWhiteSpace(v)) models.Add(v.Trim());
            }
            return models.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 2;
        }

        static void AddEvidence(ResolvedDevice r, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) r.Evidence.Add(label + ": " + value);
        }
    }
}
