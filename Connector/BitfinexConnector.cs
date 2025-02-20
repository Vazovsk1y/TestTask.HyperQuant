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
    private const string CandlesWsChannelName = "candles";
    
    private readonly ReaderWriterLockWrapper _lock = new();
    private readonly Dictionary<WebsocketChannelKey, int> _connections = [];
    private readonly HashSet<WebsocketChannelKey> _connectionRequests = [];
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

        var timeFrame = PeriodInSecToTimeFrame[periodInSec];
        using var response = await client.GetAsync($"candles/trade:{timeFrame}:{pair}/hist?{queryParamsCollection}");
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>();
        return content!.Select(e => MapToCandle(e, pair)).ToList();
    }

    public async Task<Ticker> GetTickerAsync(string pair)
    {
        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync($"ticker/{pair}");
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadFromJsonAsync<double?[]>();
        return Ticker.FromArray(content!);
    }
    
    // TODO: How to use 'maxCount'?
    public async Task SubscribeTrades(string pair, int maxCount = 100)
    {
        var key = new WebsocketChannelKey(pair, TradesWsChannelName);
        var msg = $"{{\"event\":\"subscribe\", \"channel\":\"{key.ChannelName}\", \"symbol\":\"{key.Pair}\"}}";

        await SubscribeInternal(key, msg);
    }

    // TODO: How to use 'from', 'to', 'count'?
    public async Task SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
    {
        var key = new WebsocketChannelKey(pair, CandlesWsChannelName);
        var timeFrame = PeriodInSecToTimeFrame[periodInSec];
        var msg = $"{{\"event\":\"subscribe\", \"channel\":\"{key.ChannelName}\", \"key\":\"trade:{timeFrame}:{key.Pair}\"}}";

        await SubscribeInternal(key, msg);
    }

    public void UnsubscribeCandles(string pair)
    {
        UnsubscribeInternal(pair, CandlesWsChannelName);
    }
    
    public void UnsubscribeTrades(string pair)
    {
        UnsubscribeInternal(pair, TradesWsChannelName);
    }
    
    private async Task SubscribeInternal(WebsocketChannelKey key, string message)
    {
        if (!_wsClient.IsStarted)
        {
            await _wsClient.Start();
        }

        using var lockScope = _lock.EnterWriteLock();
        if (_connections.ContainsKey(key) || !_connectionRequests.Add(key))
        {
            return;
        }

        _wsClient.Send(message);
    }

    private void UnsubscribeInternal(string pair, string channelName)
    {
        using var lockScope = _lock.EnterReadLock();

        if (!_connections.TryGetValue(new WebsocketChannelKey(pair, channelName), out var channelId))
        {
            return;
        }
        
        var msg = $"{{\"event\":\"unsubscribe\", \"chanId\":\"{channelId}\"}}";
        _wsClient.Send(msg);
        
        // TODO: Think about introducing blocking call based on 'ManualResetEvent'
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
        var item = _connections.FirstOrDefault(e => e.Value == channelId);
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
            case CandlesWsChannelName:
            {
                HandleCandlesMessage(jsonElement.Value, item.Key.Pair);
                return;
            }
        }
    }

    private void HandleCandlesMessage(JsonElement jsonElement, string pair)
    {
        var secondElement = jsonElement[1];
        switch (secondElement.ValueKind)
        {
            case JsonValueKind.Array:
            {
                var arrayLength = secondElement.GetArrayLength();
                
                // Candle update message
                const int candleArrayLength = 6;
                if (arrayLength == candleArrayLength)
                {
                    CandleSeriesProcessing?.Invoke(MapToCandle(secondElement, pair));
                    return;
                }
                
                // Snapshot
                foreach (var candle in secondElement
                             .EnumerateArray()
                             .Select(e => MapToCandle(e, pair))
                             .OrderBy(e => e.OpenTime))
                {
                    CandleSeriesProcessing?.Invoke(candle);
                }
                
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

    private static Candle MapToCandle(JsonElement tradeArray, string pair)
    {
        return new Candle
        {
            Pair = pair,
            OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(tradeArray[0].GetInt64()),
            OpenPrice = tradeArray[1].GetDecimal(),
            ClosePrice = tradeArray[2].GetDecimal(),
            HighPrice = tradeArray[3].GetDecimal(),
            LowPrice = tradeArray[4].GetDecimal(),
            TotalVolume = tradeArray[5].GetDecimal(),
        };
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
        var channelName = jsonElement.GetProperty("channel").GetString()!;
    
        var pair = jsonElement.TryGetProperty("key", out var candlesKey)
            ? candlesKey.GetString()!.Split(':')[2]
            : jsonElement.GetProperty("symbol").GetString()!;

        using var lockScope = _lock.EnterWriteLock();
        var key = new WebsocketChannelKey(pair, channelName);
        _connections[key] = chanId;
        _connectionRequests.Remove(key);
    }

    private void HandleUnsubscribedMessage(JsonElement jsonElement)
    {
        if (jsonElement.GetProperty("status").GetString() != "OK")
        {
            // TODO: Handle it the right way. For now I just ignore it. 
        }
        
        var chanId = jsonElement.GetProperty("chanId").GetInt32();

        using var lockScope = _lock.EnterWriteLock();
        var item = _connections.FirstOrDefault(e => e.Value == chanId);
        if (item.Key is null)
        {
            return;
        }
        
        _connections.Remove(item.Key);
        _connectionRequests.Remove(item.Key);
    }
}