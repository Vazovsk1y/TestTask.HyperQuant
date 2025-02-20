using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Connector.Contracts;
using Websocket.Client;

namespace Connector;

internal class BitfinexConnector : ITestConnector
{
    private record WebsocketChannelKey(string Pair, string ChannelName);
    
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
    
    private const string TradesWsChannelName = "trades";
    
    private readonly ReaderWriterLockWrapper _lock = new();
    private readonly Dictionary<WebsocketChannelKey, int> _pairToChannelInfo = [];
    private readonly HashSet<WebsocketChannelKey> _subscriptionRequests = [];
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebsocketClient _wsClient;

    internal const string HttpClientName = "BitfinexClient";

    public event Action<Trade>? NewBuyTrade;
    
    public event Action<Trade>? NewSellTrade;
    
    public event Action<Candle>? CandleSeriesProcessing;
    
    public BitfinexConnector(IHttpClientFactory httpClientFactory)
    {
        var wsClient = new WebsocketClient(new Uri("wss://api-pub.bitfinex.com/ws/2"));
        wsClient.ReconnectTimeout = TimeSpan.FromMinutes(5);
        _wsClient = wsClient;
        _wsClient.MessageReceived.Subscribe(OnWsMessageReceived);
        _httpClientFactory = httpClientFactory;
    }
    
    // NOTES:
    // - Since the task specifies that I can modify only the input parameters, I simply throw an exception if the response is not 200 OK.
    // - The public API has rate limiting; for simplicity, it is not handled.
    // - The connector should provide methods to retrieve available trading pairs, timeframes, etc. For example, a method like `GetAvailablePairs` could be useful since different exchanges may have different pair representations.
    
    public async Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
    {
        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync($"trades/{pair}/hist?limit={maxCount}");
        
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>();
        return content!.Select(e => MapToTrade(e, pair)).ToList();
    }

    public async Task<IEnumerable<Candle>> GetCandleSeriesAsync(
        string pair, 
        int periodInSec, 
        DateTimeOffset? from = null, 
        DateTimeOffset? to = null, 
        long? count = null)
    {
        using var client = _httpClientFactory.CreateClient(HttpClientName);
        
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

        if (!PeriodInSecToTimeFrame.TryGetValue(periodInSec, out var timeFrame))
        {
            // TODO: Think about throwing exception.
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
    
    // TODO: How to use maxCount?
    public async Task SubscribeTrades(string pair, int maxCount = 100)
    {
        if (!_wsClient.IsStarted)
        {
            await _wsClient.Start();
        }

        using var lockScope = _lock.EnterWriteLock();
        var key = new WebsocketChannelKey(pair, TradesWsChannelName);
        if (_pairToChannelInfo.ContainsKey(key) ||
            !_subscriptionRequests.Add(key))
        {
            return;
        }
        
        var msg = $"{{\"event\":\"subscribe\", \"channel\":\"{key.ChannelName}\", \"symbol\":\"{key.Pair}\"}}";
        _wsClient.Send(msg);
    }

    public void UnsubscribeTrades(string pair)
    {
        using var lockScope = _lock.EnterReadLock();

        if (!_pairToChannelInfo.TryGetValue(new WebsocketChannelKey(pair, TradesWsChannelName), out var channelId))
        {
            return;
        }
        
        var msg = $"{{\"event\":\"unsubscribe\", \"chanId\":\"{channelId}\"}}";
        _wsClient.Send(msg);
        
        // Think about introducing blocking call based on 'ManualResetEvent'
    }

    public void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
    {
        throw new NotImplementedException();
    }

    public void UnsubscribeCandles(string pair)
    {
        throw new NotImplementedException();
    }
    
    private void OnWsMessageReceived(ResponseMessage responseMessage)
    {
        var jsonElement = !string.IsNullOrWhiteSpace(responseMessage.Text) ? 
            JsonSerializer.Deserialize<JsonElement>(responseMessage.Text) 
            : 
            (JsonElement?)null;

        if (jsonElement is null)
        {
            return;
        }

        if (jsonElement.Value.ValueKind == JsonValueKind.Object && jsonElement.Value.TryGetProperty("event", out var eventElement))
        {
            var eventType = eventElement.GetString();
            switch (eventType)
            {
                case "subscribed":
                {
                    HandleSubscribedMessage(jsonElement.Value);
                    return;
                }
                case "unsubscribed":
                {
                    HandleUnsubscribedMessage(jsonElement.Value);
                    return;
                }
            }
        }

        if (jsonElement.Value.ValueKind != JsonValueKind.Array)
        {
            return;
        }
        
        var arrayLength = jsonElement.Value.GetArrayLength();
        if (arrayLength < 2 || jsonElement.Value[1] is { ValueKind: JsonValueKind.String } j && j.GetString() == "hb")
        {
            return;
        }

        using var lockScope = _lock.EnterReadLock();
        var channelId = jsonElement.Value[0].GetInt32();
        var item = _pairToChannelInfo.FirstOrDefault(e => e.Value == channelId);
        if (item.Key is null)
        {
            return;
        }

        switch (item.Key.ChannelName)
        {
            case TradesWsChannelName:
            {
                HandleTradesMessage(jsonElement.Value, item.Key.Pair);
                return;
            }
        }
    }

    private void HandleTradesMessage(JsonElement jsonElement, string pair)
    {
        var secondElement = jsonElement[1];
        switch (secondElement.ValueKind)
        {
            // Snapshot
            case JsonValueKind.Array:
            {
                foreach (var trade in secondElement
                             .EnumerateArray()
                             .Select(tradeArray => MapToTrade(tradeArray, pair))
                             .OrderBy(e => e.Time))
                {
                    InvokeTradeEvent(trade);
                }

                return;
            }
            // Trade update message
            case JsonValueKind.String when secondElement.GetString() is "te" or "fte":
            {
                var trade = MapToTrade(jsonElement[2], pair);
                InvokeTradeEvent(trade);
                return;
            }
            default:
                return;
        }
    }

    private static Trade MapToTrade(JsonElement tradeArray, string pair)
    {
        var amount = tradeArray[2].GetDecimal();
        var trade = new Trade
        {
            Id = tradeArray[0].GetInt32().ToString(CultureInfo.InvariantCulture),
            Pair = pair,
            Amount = amount,
            Time = DateTimeOffset.FromUnixTimeMilliseconds(tradeArray[1].GetInt64()),
            Price = tradeArray[3].GetDecimal(),
            Side = GetTradeSide(amount),
        };
        
        return trade;
    }
    
    private static string GetTradeSide(decimal amount)
    {
        return amount >= 0 ? Trade.BuySide : Trade.SellSide;
    }

    private void InvokeTradeEvent(Trade trade)
    {
        switch (trade.Side)
        {
            case Trade.BuySide:
                NewBuyTrade?.Invoke(trade);
                return;
            case Trade.SellSide:
                NewSellTrade?.Invoke(trade);
                return;
        }
    }
    
    private void HandleSubscribedMessage(JsonElement jsonElement)
    {
        var chanId = jsonElement.GetProperty("chanId").GetInt32();
        var symbol = jsonElement.GetProperty("symbol").GetString()!;
        var channelName = jsonElement.GetProperty("channel").GetString()!;

        using var lockScope = _lock.EnterWriteLock();
        var key = new WebsocketChannelKey(symbol, channelName);
        _pairToChannelInfo[key] = chanId;
        _subscriptionRequests.Remove(key);
    }

    private void HandleUnsubscribedMessage(JsonElement jsonElement)
    {
        if (jsonElement.GetProperty("status").GetString() != "OK")
        {
            // TODO: Handle it the right way.
        }
        
        var chanId = jsonElement.GetProperty("chanId").GetInt32();

        using var lockScope = _lock.EnterWriteLock();
        var item = _pairToChannelInfo.FirstOrDefault(e => e.Value == chanId);
        if (item.Key is null)
        {
            return;
        }
        
        _pairToChannelInfo.Remove(item.Key);
        _subscriptionRequests.Remove(item.Key);
    }
}