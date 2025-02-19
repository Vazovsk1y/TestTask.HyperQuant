using System.Globalization;
using System.Net.Http.Json;
using System.Web;
using Connector.Contracts;

namespace Connector;

internal class BitfinexConnector(IHttpClientFactory httpClientFactory) : ITestConnector
{
    public event Action<Trade>? NewBuyTrade;
    public event Action<Trade>? NewSellTrade;
    public event Action<Candle>? CandleSeriesProcessing;
    
    internal const string HttpClientName = "BitfinexClient";
    
    private static readonly Dictionary<int, string> PeriodInSecToTimeFrame = new()
    {
        { 60, "1m" },
        { 300, "5m" },
        { 900, "15m" },
        { 1800, "30m" },
        { 3600, "1h" },
        { 10800, "3h" },
        { 21600, "6h" },
        { 43200, "12h" },
        { 86400, "1D" },
        { 604800, "1W" },
        { 1209600, "14D" },
        { 2592000, "1M" },
    };
    
    // NOTES:
    // - Since the task specifies that I can modify only the input parameters, I simply throw an exception if the response is not 200 OK.
    // - The public API has rate limiting; for simplicity, it is not handled.
    // - The connector should provide methods to retrieve available trading pairs, timeframes, etc. For example, a method like `GetAvailablePairs` could be useful since different exchanges may have different pair representations.
    
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

    public async Task<IEnumerable<Candle>> GetCandleSeriesAsync(
        string pair, 
        int periodInSec, 
        DateTimeOffset? from = null, 
        DateTimeOffset? to = null, 
        long? count = null)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        
        var queryParamsCollection = HttpUtility.ParseQueryString(string.Empty);
        if (from.HasValue)
        {
            queryParamsCollection["from"] = from.Value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        }

        if (to.HasValue)
        {
            queryParamsCollection["end"] = to.Value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        }

        if (count > 0)
        {
            queryParamsCollection["limit"] = count.Value.ToString(CultureInfo.InvariantCulture);
        }

        // TODO: Think about throwing exception.
        if (!PeriodInSecToTimeFrame.TryGetValue(periodInSec, out var timeFrame))
        {
            timeFrame = PeriodInSecToTimeFrame.First().Value;
        }
        
        using var response = await client.GetAsync($"candles/trade:{timeFrame}:{pair}/hist?{queryParamsCollection}");
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<decimal[]>>();
        return content!.Select(e => new Candle
        {
            Pair = pair,
            OpenPrice = e[1],
            HighPrice = e[3],
            LowPrice = e[4],
            ClosePrice = e[2],
            TotalVolume = e[5],
            OpenTime = DateTimeOffset.FromUnixTimeMilliseconds((long)e[0]),
        })
        .ToList();
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