using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace BebelEquipe155
{
    public sealed class MarketCache
    {
        readonly string dir;
        readonly JavaScriptSerializer json = new JavaScriptSerializer();
        public MarketCache(string dir) { this.dir=dir; Directory.CreateDirectory(dir); json.MaxJsonLength=int.MaxValue; }

        string PathFor(MarketQuery q)
        {
            string raw=(q.SiteId ?? "")+"|"+(q.Manufacturer ?? "")+"|"+(q.CommercialName ?? "")+"|"+(q.StorageGb.HasValue?q.StorageGb.Value.ToString():"");
            using(SHA256 sha=SHA256.Create())
            {
                byte[] h=sha.ComputeHash(Encoding.UTF8.GetBytes(raw.ToLowerInvariant())); StringBuilder sb=new StringBuilder(); foreach(byte b in h) sb.Append(b.ToString("x2"));
                return Path.Combine(dir,sb.ToString()+".json");
            }
        }

        public void Save(MarketQuote quote)
        {
            if(quote==null || quote.Query==null) return;
            string path=PathFor(quote.Query), tmp=path+".tmp";
            File.WriteAllText(tmp,json.Serialize(quote),new UTF8Encoding(false));
            if(File.Exists(path)) File.Delete(path);
            File.Move(tmp,path);
        }

        public MarketQuote LoadFresh(MarketQuery query, TimeSpan ttl)
        {
            MarketQuote q=LoadAny(query); if(q==null) return null;
            return q.CacheAge<=ttl ? q : null;
        }

        public MarketQuote LoadAny(MarketQuery query)
        {
            try
            {
                string path=PathFor(query); if(!File.Exists(path)) return null;
                MarketQuote q=json.Deserialize<MarketQuote>(File.ReadAllText(path,Encoding.UTF8)); if(q==null) return null;
                q.IsFromCache=true; q.CacheAge=DateTime.UtcNow-q.RetrievedUtc; if(q.CacheAge<TimeSpan.Zero) q.CacheAge=TimeSpan.Zero; return q;
            }
            catch { return null; }
        }
    }
}
