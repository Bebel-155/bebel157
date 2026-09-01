using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace BebelEquipe155
{
    public sealed class MercadoLivreHttpClient : IMarketHttpClient
    {
        public MarketHttpResponse Get(string url, string bearerToken)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 12000;
            request.ReadWriteTimeout = 12000;
            request.UserAgent = "BebelEquipe155/5.2";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            if (!string.IsNullOrWhiteSpace(bearerToken)) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + bearerToken.Trim();
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    return new MarketHttpResponse { StatusCode = (int)response.StatusCode, Body = reader.ReadToEnd() };
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response == null) throw new MarketNetworkException("Falha de rede ao consultar o Mercado Livre.");
                string body = "";
                try { using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) body = reader.ReadToEnd(); } catch { }
                return new MarketHttpResponse { StatusCode = (int)response.StatusCode, Body = body };
            }
        }
    }

    public sealed class MercadoLivreAdapter : IMarketAdapter
    {
        readonly string accessToken;
        readonly IMarketHttpClient http;
        readonly JavaScriptSerializer json = new JavaScriptSerializer();

        public MercadoLivreAdapter(string accessToken) : this(accessToken, new MercadoLivreHttpClient()) { }
        public MercadoLivreAdapter(string accessToken, IMarketHttpClient http)
        {
            this.accessToken = accessToken ?? "";
            this.http = http;
            json.MaxJsonLength = int.MaxValue;
        }

        public IEnumerable<MarketListing> Search(MarketQuery query, MarketCondition condition)
        {
            if (query == null) throw new ArgumentNullException("query");
            string url = "https://api.mercadolibre.com/sites/" + Uri.EscapeDataString(query.SiteId ?? "MLB") + "/search?q=" + Uri.EscapeDataString(query.SearchText()) + "&limit=50";
            MarketHttpResponse response = http.Get(url, accessToken);
            if (response.StatusCode == 401 || response.StatusCode == 403) throw new MarketAuthorizationRequiredException("Mercado Livre requer autorização para esta consulta.");
            if (response.StatusCode == 429) throw new MarketRateLimitException("Limite de consultas do Mercado Livre atingido.");
            if (response.StatusCode < 200 || response.StatusCode >= 300) throw new MarketNetworkException("Mercado Livre respondeu HTTP " + response.StatusCode + ".");

            Dictionary<string,object> root = json.DeserializeObject(response.Body ?? "{}") as Dictionary<string,object>;
            List<MarketListing> result = new List<MarketListing>();
            if (root == null || !root.ContainsKey("results")) return result;
            IEnumerable items = root["results"] as IEnumerable;
            if (items == null) return result;
            foreach (object itemObj in items)
            {
                Dictionary<string,object> item = itemObj as Dictionary<string,object>;
                if (item == null) continue;
                MarketCondition parsed = ParseCondition(item);
                if (parsed != condition) continue;
                decimal price;
                if (!TryDecimal(Get(item,"price"), out price) || price <= 0) continue;
                result.Add(new MarketListing {
                    Source="Mercado Livre", Id=Get(item,"id"), Title=Get(item,"title"), Price=price,
                    Currency=Get(item,"currency_id"), Condition=parsed, Url=Get(item,"permalink")
                });
            }
            return result;
        }

        static string Get(Dictionary<string,object> d, string key)
        {
            object value;
            return d != null && d.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }

        static bool TryDecimal(string text, out decimal value)
        {
            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        static MarketCondition ParseCondition(Dictionary<string,object> item)
        {
            object attrsObj;
            if (item.TryGetValue("attributes", out attrsObj))
            {
                IEnumerable attrs = attrsObj as IEnumerable;
                if (attrs != null)
                {
                    foreach (object attrObj in attrs)
                    {
                        Dictionary<string,object> attr = attrObj as Dictionary<string,object>;
                        if (attr == null || !Get(attr,"id").Equals("ITEM_CONDITION",StringComparison.OrdinalIgnoreCase)) continue;
                        MarketCondition c = ConditionFromText(Get(attr,"value_name"));
                        if (c != MarketCondition.Unknown) return c;
                        c = ConditionFromText(Get(attr,"value_id"));
                        if (c != MarketCondition.Unknown) return c;
                    }
                }
            }
            return ConditionFromText(Get(item,"condition"));
        }

        static MarketCondition ConditionFromText(string value)
        {
            string v=(value ?? "").Trim().ToLowerInvariant();
            if (v=="new" || v.Contains("novo")) return MarketCondition.New;
            if (v=="used" || v.Contains("usado")) return MarketCondition.Used;
            if (v.Contains("refurb") || v.Contains("recond")) return MarketCondition.Refurbished;
            return MarketCondition.Unknown;
        }
    }
}
