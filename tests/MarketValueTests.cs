using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BebelEquipe155;

class FakeMarketHttp : IMarketHttpClient
{
    public MarketHttpResponse Response;
    public readonly List<string> Urls = new List<string>();
    public MarketHttpResponse Get(string url, string bearerToken)
    {
        Urls.Add(url);
        return Response;
    }
}

class FakeMarketAdapter : IMarketAdapter
{
    public int Calls;
    public bool Fail;
    public List<MarketListing> NewListings = new List<MarketListing>();
    public List<MarketListing> UsedListings = new List<MarketListing>();
    public IEnumerable<MarketListing> Search(MarketQuery query, MarketCondition condition)
    {
        Calls++;
        if (Fail) throw new MarketNetworkException("offline");
        return condition == MarketCondition.New ? NewListings : UsedListings;
    }
}

class MarketValueTests
{
    static int failures;
    static void Main()
    {
        Run("MarketQueryContainsNoDeviceIdentifier", MarketQueryContainsNoDeviceIdentifier);
        Run("NormalizerRejectsAccessoriesStorageAndOutlier", NormalizerRejectsAccessoriesStorageAndOutlier);
        Run("MercadoLivreParsesConditions", MercadoLivreParsesConditions);
        Run("CacheTtl", CacheTtl);
        Run("ServiceUsesFreshCache", ServiceUsesFreshCache);
        Run("ServiceFallsBackToStaleCache", ServiceFallsBackToStaleCache);
        Environment.Exit(failures == 0 ? 0 : 1);
    }
    static void Run(string n, Action a) { try { a(); Console.WriteLine("PASS " + n); } catch(Exception ex) { failures++; Console.WriteLine("FAIL " + n + ": " + ex.Message); } }

    static ResolvedDevice Phone()
    {
        return new ResolvedDevice { Manufacturer="Samsung", CommercialName="Galaxy S24", StorageVariant="256 GB", Confidence=MatchConfidence.Exact, Device=new DeviceRef { TransportId="R58SECRET", Platform=DevicePlatform.Android } };
    }

    static void MarketQueryContainsNoDeviceIdentifier()
    {
        MarketQuery q = MarketQuery.FromDevice(Phone(), "MLB", "BRL");
        string text=q.ToLogString();
        TestAssert.True(text.IndexOf("R58SECRET",StringComparison.OrdinalIgnoreCase)<0,"identifier leaked");
        TestAssert.Contains("Galaxy S24",text,"model missing");
        TestAssert.Equal(256, q.StorageGb.Value, "storage");
    }

    static void NormalizerRejectsAccessoriesStorageAndOutlier()
    {
        MarketQuery q = MarketQuery.FromDevice(Phone(), "MLB", "BRL");
        List<MarketListing> x = new List<MarketListing>();
        decimal[] prices={3200,3300,3400,3500,3600,12000};
        foreach(decimal p in prices) x.Add(new MarketListing { Source="ML",Title="Samsung Galaxy S24 256GB 5G",Price=p,Currency="BRL",Condition=MarketCondition.Used });
        x.Add(new MarketListing { Source="ML",Title="Capa Galaxy S24 256GB",Price=100,Currency="BRL",Condition=MarketCondition.Used });
        x.Add(new MarketListing { Source="ML",Title="Galaxy S24 128GB",Price=2500,Currency="BRL",Condition=MarketCondition.Used });
        x.Add(new MarketListing { Source="ML",Title="Tela Display Galaxy S24 256GB",Price=800,Currency="BRL",Condition=MarketCondition.Used });
        MarketEstimate e = new PriceNormalizer().Calculate(q, MarketCondition.Used, x);
        TestAssert.Equal(5,e.ValidListingCount,"valid count");
        TestAssert.Equal(3400m,e.Median,"median");
        TestAssert.True(e.Max < 12000m,"outlier not removed");
    }

    static void MercadoLivreParsesConditions()
    {
        string json="{\"results\":[{\"id\":\"MLB1\",\"title\":\"Galaxy S24 256GB\",\"price\":3999.9,\"currency_id\":\"BRL\",\"permalink\":\"https://x/1\",\"condition\":\"new\",\"attributes\":[{\"id\":\"ITEM_CONDITION\",\"value_name\":\"Novo\"}]},{\"id\":\"MLB2\",\"title\":\"Galaxy S24 256GB\",\"price\":2999,\"currency_id\":\"BRL\",\"permalink\":\"https://x/2\",\"condition\":\"used\"}]}";
        FakeMarketHttp h=new FakeMarketHttp { Response=new MarketHttpResponse { StatusCode=200,Body=json } };
        MercadoLivreAdapter a=new MercadoLivreAdapter("",h);
        List<MarketListing> n=a.Search(MarketQuery.FromDevice(Phone(),"MLB","BRL"),MarketCondition.New).ToList();
        List<MarketListing> u=a.Search(MarketQuery.FromDevice(Phone(),"MLB","BRL"),MarketCondition.Used).ToList();
        TestAssert.Equal(1,n.Count,"new count"); TestAssert.Equal(1,u.Count,"used count");
    }

    static void CacheTtl()
    {
        string dir=Path.Combine(Path.GetTempPath(),"B155Market_"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        MarketCache c=new MarketCache(dir);
        MarketQuery q=MarketQuery.FromDevice(Phone(),"MLB","BRL");
        MarketQuote quote=new MarketQuote { Query=q,RetrievedUtc=DateTime.UtcNow.AddHours(-5).AddMinutes(-59),Source="test" };
        c.Save(quote); TestAssert.True(c.LoadFresh(q,TimeSpan.FromHours(6))!=null,"5h59 should be fresh");
        quote.RetrievedUtc=DateTime.UtcNow.AddHours(-6).AddMinutes(-1); c.Save(quote);
        TestAssert.True(c.LoadFresh(q,TimeSpan.FromHours(6))==null,"6h01 should be stale");
        TestAssert.True(c.LoadAny(q)!=null,"stale should remain");
        try { Directory.Delete(dir,true); } catch { }
    }

    static void ServiceUsesFreshCache()
    {
        string dir=Path.Combine(Path.GetTempPath(),"B155Market_"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        MarketCache c=new MarketCache(dir); MarketQuery q=MarketQuery.FromDevice(Phone(),"MLB","BRL"); c.Save(new MarketQuote { Query=q,RetrievedUtc=DateTime.UtcNow,Source="cache" });
        FakeMarketAdapter a=new FakeMarketAdapter(); MarketPriceService s=new MarketPriceService(a,new PriceNormalizer(),c);
        MarketQuote got=s.GetQuote(Phone(),false); TestAssert.Equal(0,a.Calls,"adapter calls"); TestAssert.True(got.IsFromCache,"cache flag");
        try { Directory.Delete(dir,true); } catch { }
    }

    static void ServiceFallsBackToStaleCache()
    {
        string dir=Path.Combine(Path.GetTempPath(),"B155Market_"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        MarketCache c=new MarketCache(dir); MarketQuery q=MarketQuery.FromDevice(Phone(),"MLB","BRL"); c.Save(new MarketQuote { Query=q,RetrievedUtc=DateTime.UtcNow.AddDays(-1),Source="cache" });
        FakeMarketAdapter a=new FakeMarketAdapter { Fail=true }; MarketPriceService s=new MarketPriceService(a,new PriceNormalizer(),c);
        MarketQuote got=s.GetQuote(Phone(),true); TestAssert.True(got!=null && got.IsFromCache,"stale fallback");
        try { Directory.Delete(dir,true); } catch { }
    }
}
