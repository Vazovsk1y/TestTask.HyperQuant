namespace Connector.Contracts;

// Ticker class was not provided.
public class Ticker
{
    public double? Bid { get; set; }
    public double? BidSize { get; set; }
    public double? Ask { get; set; }
    public double? AskSize { get; set; }
    public double? DailyChange { get; set; }
    public double? DailyChangeRelative { get; set; }
    public double? LastPrice { get; set; }
    public double? Volume { get; set; }
    public double? High { get; set; }
    public double? Low { get; set; }
    public double? FRR { get; set; }
    public int? BidPeriod { get; set; }
    public int? AskPeriod { get; set; }
    public double? FRRAmountAvailable { get; set; }

    public static Ticker FromArray(double?[] data)
    {
        if (data.Length >= 10)
        {
            return new Ticker
            {
                Bid = data[0],
                BidSize = data[1],
                Ask = data[2],
                AskSize = data[3],
                DailyChange = data[4],
                DailyChangeRelative = data[5],
                LastPrice = data[6],
                Volume = data[7],
                High = data[8],
                Low = data[9]
            };
        }

        if (data.Length >= 13)
        {
            return new Ticker
            {
                FRR = data[0],
                Bid = data[1],
                BidPeriod = (int?)data[2],
                BidSize = data[3],
                Ask = data[4],
                AskPeriod = (int?)data[5],
                AskSize = data[6],
                DailyChange = data[7],
                DailyChangeRelative = data[8],
                LastPrice = data[9],
                Volume = data[10],
                High = data[11],
                Low = data[12],
                FRRAmountAvailable = data.Length > 15 ? data[15] : null
            };
        }
        
        throw new ArgumentException("Unexpected ticker data format.");
    }
}