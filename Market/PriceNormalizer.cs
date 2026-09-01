using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BebelEquipe155
{
    public sealed class PriceNormalizer
    {
        static readonly string[] RejectWords = { "capa", "case", "pelicula", "tela", "display", "lcd", "bateria", "placa", "peca", "conector", "carcaca", "lote", "somente pecas", "retirada de pecas" };

        public MarketEstimate Calculate(MarketQuery query, MarketCondition condition, IEnumerable<MarketListing> listings)
        {
            List<MarketListing> valid = new List<MarketListing>();
            foreach (MarketListing listing in listings ?? new MarketListing[0])
            {
                if (listing == null || listing.Price <= 0 || listing.Condition != condition) continue;
                string title = Normalize(listing.Title);
                if (RejectWords.Any(w => title.Contains(w))) continue;
                double score = ModelScore(query, title);
                if (score < 0.85) continue;
                bool storage = StorageMatches(query, title);
                if (!storage) continue;
                listing.ModelMatchScore = score;
                listing.StorageMatch = storage;
                valid.Add(listing);
            }

            List<decimal> prices = valid.Select(x => x.Price).OrderBy(x => x).ToList();
            if (prices.Count >= 5)
            {
                decimal q1 = Median(prices.Take(prices.Count / 2).ToList());
                decimal q3 = Median(prices.Skip((prices.Count + 1) / 2).ToList());
                decimal iqr = q3 - q1;
                decimal low = q1 - 1.5m * iqr, high = q3 + 1.5m * iqr;
                prices = prices.Where(p => p >= low && p <= high).ToList();
            }
            if (prices.Count == 0) return new MarketEstimate { Condition=condition, ValidListingCount=0 };
            return new MarketEstimate { Condition=condition, ValidListingCount=prices.Count, Median=Median(prices), Min=prices.First(), Max=prices.Last() };
        }

        static decimal Median(List<decimal> values)
        {
            if (values == null || values.Count == 0) return 0;
            values = values.OrderBy(x => x).ToList();
            int n=values.Count;
            return n%2==1 ? values[n/2] : (values[n/2-1]+values[n/2])/2m;
        }

        static double ModelScore(MarketQuery query, string normalizedTitle)
        {
            string name = Normalize((query == null ? "" : query.CommercialName) ?? "");
            string maker = Normalize((query == null ? "" : query.Manufacturer) ?? "");
            List<string> tokens = Regex.Matches(name,@"[a-z0-9]+") .Cast<Match>().Select(m=>m.Value).Where(t=>t.Length>1).Distinct().ToList();
            if (tokens.Count == 0) return 0;
            int found=tokens.Count(t=>Regex.IsMatch(normalizedTitle,@"(^|\s)"+Regex.Escape(t)+@"($|\s)"));
            double score=(double)found/tokens.Count;
            if (!string.IsNullOrWhiteSpace(maker) && normalizedTitle.Contains(maker)) score=Math.Min(1.0,score+0.05);
            return score;
        }

        static bool StorageMatches(MarketQuery query, string normalizedTitle)
        {
            if (query == null || !query.StorageGb.HasValue) return true;
            int target=query.StorageGb.Value;
            MatchCollection m=Regex.Matches(normalizedTitle,@"\b(\d{2,4})\s*(gb|tb)\b",RegexOptions.IgnoreCase);
            if (m.Count==0) return false;
            foreach(Match x in m)
            {
                int v; if(!int.TryParse(x.Groups[1].Value,out v)) continue;
                if (x.Groups[2].Value.Equals("tb",StringComparison.OrdinalIgnoreCase)) v*=1024;
                if(v==target) return true;
            }
            return false;
        }

        public static string Normalize(string value)
        {
            string d=(value ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb=new StringBuilder();
            foreach(char c in d)
            {
                UnicodeCategory cat=CharUnicodeInfo.GetUnicodeCategory(c);
                if(cat==UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
            return Regex.Replace(sb.ToString(),@"\s+"," ").Trim();
        }
    }
}
