using System.Globalization;
using System.Net.Http.Json;
using Connector.Contracts;

namespace Connector;

internal class BitfinexConnector(IHttpClientFactory httpClientFactory) : ITestConnector
{
    public event Action<Trade>? NewBuyTrade;
    public event Action<Trade>? NewSellTrade;
    public event Action<Candle>? CandleSeriesProcessing;
    
    internal const string HttpClientName = "BitfinexClient";
    
    // NOTES:
    // - Since the task specifies that I can modify only the input parameters, I simply throw an exception if the response is not 200 OK.
    // - The public API has rate limiting; for simplicity, it is not handled.
    
    public async Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync($"trades/{pair}/hist?limit={maxCount}");
        
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<IEnumerable<decimal[]>>();
        return content!.Select(e => new Trade
        {
            Id = e[0].ToString(CultureInfo.InvariantCulture),
            Pair = pair,
            Time = DateTimeOffset.FromUnixTimeMilliseconds((long)e[1]),
            Amount = e[2],
            Price = e[3],
            Side = e[2] > 0 ? "buy" : "sell",
        }).ToList();
    }

    public Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from, DateTimeOffset? to = null, long? count = 0)
    {
        throw new NotImplementedException();
    }
    
    public void SubscribeTrades(string pair, int maxCount = 100)
    {
        throw new NotImplementedException();
    }

    public void UnsubscribeTrades(string pair)
    {
        throw new NotImplementedException();
    }

    public void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
    {
        throw new NotImplementedException();
    }

    public void UnsubscribeCandles(string pair)
    {
        throw new NotImplementedException();
    }
}