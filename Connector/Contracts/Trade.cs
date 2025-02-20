namespace Connector.Contracts;

public class Trade
{
    internal const string BuySide = "buy";
    internal const string SellSide = "sell";
    
    /// <summary>
    /// Валютная пара
    /// </summary>
    public string Pair { get; set; }

    /// <summary>
    /// Цена трейда
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Объем трейда
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Направление (buy/sell)
    /// </summary>
    public string Side { get; set; }

    /// <summary>
    /// Время трейда
    /// </summary>
    public DateTimeOffset Time { get; set; }


    /// <summary>
    /// Id трейда
    /// </summary>
    public string Id { get; set; }
}