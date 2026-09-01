using System;
using System.Collections.Generic;
using System.Linq;

namespace BebelEquipe155
{
    public sealed class MarketPriceService
    {
        readonly IMarketAdapter adapter;
        readonly PriceNormalizer normalizer;
        readonly MarketCache cache;
        readonly TimeSpan ttl=TimeSpan.FromHours(6);

        public MarketPriceService(IMarketAdapter adapter, PriceNormalizer normalizer, MarketCache cache)
        {
            this.adapter=adapter; this.normalizer=normalizer; this.cache=cache;
        }

        public MarketQuote GetQuote(ResolvedDevice device, bool forceRefresh)
        {
            MarketQuery query=MarketQuery.FromDevice(device,"MLB","BRL");
            if(!forceRefresh)
            {
                MarketQuote fresh=cache.LoadFresh(query,ttl);
                if(fresh!=null) { fresh.StatusMessage="Cotação carregada do cache local."; return fresh; }
            }
            try
            {
                List<MarketListing> newer=adapter.Search(query,MarketCondition.New).ToList();
                List<MarketListing> used=adapter.Search(query,MarketCondition.Used).ToList();
                MarketEstimate ne=normalizer.Calculate(query,MarketCondition.New,newer);
                MarketEstimate ue=normalizer.Calculate(query,MarketCondition.Used,used);
                if(ne.ValidListingCount==0 && ue.ValidListingCount==0) throw new MarketUnavailableException("Nenhum anúncio compatível encontrado para modelo/capacidade.");
                MarketQuote quote=new MarketQuote { Query=query,NewEstimate=ne,UsedEstimate=ue,RetrievedUtc=DateTime.UtcNow,Source="Mercado Livre",IsFromCache=false,CacheAge=TimeSpan.Zero,StatusMessage="Estimativa de mercado atualizada." };
                cache.Save(quote); return quote;
            }
            catch(Exception ex)
            {
                MarketQuote stale=cache.LoadAny(query);
                if(stale!=null)
                {
                    stale.StatusMessage="Consulta online indisponível; exibindo a última cotação válida. " + SafeMessage(ex);
                    return stale;
                }
                throw;
            }
        }

        static string SafeMessage(Exception ex)
        {
            if(ex is MarketAuthorizationRequiredException) return "Autorização do Mercado Livre necessária.";
            if(ex is MarketRateLimitException) return "Limite temporário de consultas atingido.";
            if(ex is MarketNetworkException) return "Sem acesso ao serviço de preços.";
            return ex.Message;
        }
    }
}
