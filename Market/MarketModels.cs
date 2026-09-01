using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace BebelEquipe155
{
    public enum MarketCondition { New, Used, Refurbished, Unknown }

    public sealed class MarketQuery
    {
        public string Manufacturer { get; set; }
        public string CommercialName { get; set; }
        public int? StorageGb { get; set; }
        public string SiteId { get; set; }
        public string Currency { get; set; }

        public static MarketQuery FromDevice(ResolvedDevice device, string siteId, string currency)
        {
            if (device == null) throw new ArgumentNullException("device");
            if (device.Confidence == MatchConfidence.Incomplete) throw new MarketUnavailableException("Identificação incompleta: cotação automática bloqueada.");
            if (string.IsNullOrWhiteSpace(device.CommercialName)) throw new MarketUnavailableException("Modelo comercial não identificado.");
            MarketQuery q = new MarketQuery();
            q.Manufacturer = device.Manufacturer ?? "";
            q.CommercialName = device.CommercialName.Trim();
            q.StorageGb = ParseStorageGb(device.StorageVariant);
            if (!q.StorageGb.HasValue && device.StorageTotalBytes.HasValue)
                q.StorageGb = NearestCommercialStorage(device.StorageTotalBytes.Value);
            q.SiteId = string.IsNullOrWhiteSpace(siteId) ? "MLB" : siteId.Trim().ToUpperInvariant();
            q.Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant();
            return q;
        }

        static int? ParseStorageGb(string value)
        {
            Match m = Regex.Match(value ?? "", @"(\d+)\s*(?:GB|GIB)", RegexOptions.IgnoreCase);
            int v;
            if (m.Success && int.TryParse(m.Groups[1].Value, out v)) return v;
            Match t = Regex.Match(value ?? "", @"(\d+)\s*(?:TB|TIB)", RegexOptions.IgnoreCase);
            if (t.Success && int.TryParse(t.Groups[1].Value, out v)) return v * 1024;
            return null;
        }

        static int? NearestCommercialStorage(long bytes)
        {
            if (bytes <= 0) return null;
            double gb = bytes / 1000000000.0;
            int[] candidates = { 16, 32, 64, 128, 256, 512, 1024, 2048 };
            int best = candidates[0]; double delta = Math.Abs(gb - best);
            foreach (int c in candidates) { double d = Math.Abs(gb - c); if (d < delta) { delta = d; best = c; } }
            return delta <= best * 0.22 ? (int?)best : null;
        }

        public string SearchText()
        {
            string s = ((Manufacturer ?? "") + " " + (CommercialName ?? "")).Trim();
            if (StorageGb.HasValue) s += " " + StorageGb.Value.ToString(CultureInfo.InvariantCulture) + "GB";
            return s.Trim();
        }

        public string ToLogString()
        {
            return SearchText() + " / " + (SiteId ?? "MLB");
        }
    }

    public sealed class MarketListing
    {
        public string Source { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public MarketCondition Condition { get; set; }
        public string Url { get; set; }
        public double ModelMatchScore { get; set; }
        public bool StorageMatch { get; set; }
    }

    public sealed class MarketEstimate
    {
        public MarketCondition Condition { get; set; }
        public decimal Median { get; set; }
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public int ValidListingCount { get; set; }
    }

    public sealed class MarketQuote
    {
        public MarketQuery Query { get; set; }
        public MarketEstimate NewEstimate { get; set; }
        public MarketEstimate UsedEstimate { get; set; }
        public DateTime RetrievedUtc { get; set; }
        public string Source { get; set; }
        public string StatusMessage { get; set; }
        [ScriptIgnore] public bool IsFromCache { get; set; }
        [ScriptIgnore] public TimeSpan CacheAge { get; set; }

        [ScriptIgnore]
        public decimal? ReferenceValue
        {
            get
            {
                if (UsedEstimate != null && UsedEstimate.ValidListingCount > 0) return UsedEstimate.Median;
                if (NewEstimate != null && NewEstimate.ValidListingCount > 0) return NewEstimate.Median;
                return null;
            }
        }
    }

    public class MarketUnavailableException : Exception { public MarketUnavailableException(string message) : base(message) { } }
    public class MarketNetworkException : Exception { public MarketNetworkException(string message) : base(message) { } }
    public class MarketAuthorizationRequiredException : Exception { public MarketAuthorizationRequiredException(string message) : base(message) { } }
    public class MarketRateLimitException : Exception { public MarketRateLimitException(string message) : base(message) { } }
}
