﻿namespace Connector.Contracts;

public interface ITestConnector
{
    #region Rest

    Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount);
    Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = null);
    
    // Added for implementing ticker information obtaining use case.
    Task<Ticker> GetTickerAsync(string pair);
    
    // Added for implementing balances calculation use case.
    Task<decimal?> CalculateExchangeRateAsync(string fromCurrency, string toCurrency);

    #endregion

    #region Socket

    event Action<Trade> NewBuyTrade;
    event Action<Trade> NewSellTrade;
    
    // Moved from void -> Task to provide async API.
    Task SubscribeTrades(string pair, int maxCount = 100);
    
    void UnsubscribeTrades(string pair);

    event Action<Candle> CandleSeriesProcessing;
    
    // Moved from void -> Task to provide async API.
    Task SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0);
    
    void UnsubscribeCandles(string pair);

    #endregion
}