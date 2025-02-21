using Connector.Contracts;

namespace Connector.WPF.ViewModels;

public record TickerViewModel(
    double? Bid,
    double? BidSize,
    double? Ask,
    double? AskSize,
    double? DailyChange,
    double? DailyChangeRelative,
    double? LastPrice,
    double? Volume,
    double? High,
    double? Low,
    double? FRR = null,
    int? BidPeriod = null,
    int? AskPeriod = null,
    double? FRRAmountAvailable = null)
{
    public static TickerViewModel MapFromModel(Ticker model)
    {
        return new TickerViewModel(
            model.Bid,
            model.BidSize,
            model.Ask,
            model.AskSize,
            model.DailyChange,
            model.DailyChangeRelative,
            model.LastPrice,
            model.Volume,
            model.High,
            model.Low,
            model.FRR,
            model.BidPeriod,
            model.AskPeriod,
            model.FRRAmountAvailable
        );
    }
}