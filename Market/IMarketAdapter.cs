using System.Collections.Generic;

namespace BebelEquipe155
{
    public interface IMarketAdapter
    {
        IEnumerable<MarketListing> Search(MarketQuery query, MarketCondition condition);
    }

    public sealed class MarketHttpResponse
    {
        public int StatusCode { get; set; }
        public string Body { get; set; }
    }

    public interface IMarketHttpClient
    {
        MarketHttpResponse Get(string url, string bearerToken);
    }
}
