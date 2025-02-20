using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Connector.Contracts;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Connector.WPF.ViewModels;

public partial class RestPageViewModel(ITestConnector connector) : ObservableObject
{
    public PlotModel CandlesChart { get; } = new()
    {
        Title = "Candles Chart",
        Axes =
        {
            new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "dd.MM.yyyy HH:mm",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                IntervalType = DateTimeIntervalType.Auto,
                Title = "Time",
            },
        },
        Series =
        {
            new CandleStickSeries
            {
                Color = OxyColors.Black,
                IncreasingColor = OxyColors.Green,
                DecreasingColor = OxyColors.Red,
                TrackerFormatString = "{0}\nDateTime: {2:dd.MM.yyyy HH:mm}\nHigh: {3:0.###}\nLow: {4:0.###}\nOpen: {5:0.###}\nClose: {6:0.###}",
            },
        }
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetTickerCommand))]
    private string? _tickerPair;
    
    [ObservableProperty]
    private TickerViewModel? _tickerViewModel;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetTradesCommand))]
    private uint _tradesLimit = 125;
    
    [ObservableProperty]
    private IReadOnlyCollection<TradeViewModel>? _trades;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetTradesCommand))]
    private string? _tradesPair;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetCandleSeriesCommand))]
    private string? _candlesPair;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetCandleSeriesCommand))]
    private uint _candlesLimit = 125;

    [ObservableProperty]
    private KeyValuePair<string, int> _candlesTimeFrame = AvailableTimeFrames.First();
    
    [ObservableProperty]
    private DateTime _candlesFrom = DateTime.Now.AddDays(-7);

    [ObservableProperty] 
    private DateTime _candlesTo = DateTime.Now;
    
    public static readonly IEnumerable<KeyValuePair<string, int>> AvailableTimeFrames = new List<KeyValuePair<string, int>>
    {
        new("1m", 60),
        new("5m", 300),
        new("15m", 900),
        new("30m", 1800),
        new("1h", 3600),
        new("3h", 10800),
        new("6h", 21600),
        new("12h", 43200),
        new("1D", 86400),
        new("1W", 604800),
        new("14D", 1209600),
        new("1M", 2592000)
    };

    [RelayCommand(CanExecute = nameof(CanGetTicker))]
    private async Task GetTicker()
    {
        try
        {
            var result = await connector.GetTickerAsync(TickerPair!);
            TickerViewModel = TickerViewModel.MapFromModel(result);
        }
        catch (HttpRequestException e)
        {
            MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanGetTicker() => !string.IsNullOrWhiteSpace(TickerPair);
    
    [RelayCommand(CanExecute = nameof(CanGetTrades))]
    private async Task GetTrades()
    {
        try
        {
            var result = await connector.GetNewTradesAsync(TradesPair!, (int)TradesLimit);
            Trades = result
                .Select((e, i) => new TradeViewModel(
                    i + 1, 
                    e.Pair, 
                    e.Amount, 
                    e.Price, 
                    e.Side, 
                    e.Time.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"), 
                    e.Id))
                .ToList();
        }
        catch (HttpRequestException e)
        {
            MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanGetTrades() => !string.IsNullOrWhiteSpace(TradesPair) && 
                                   TradesLimit >= 1;

    [RelayCommand(CanExecute = nameof(CanGetCandleSeries))]
    private async Task GetCandleSeries()
    {
        try
        {
            var from = CandlesFrom.ToUniversalTime();
            var to = CandlesFrom.ToUniversalTime();

            var result = await connector.GetCandleSeriesAsync(CandlesPair!, CandlesTimeFrame.Value, from, to, CandlesLimit);
            
            var series = CandlesChart.Series.OfType<CandleStickSeries>().First();
            series.Items.Clear();
            
            series.Items.AddRange(result
                .OrderBy(e => e.OpenTime)
                .Select(candle => new HighLowItem(
                    DateTimeAxis.ToDouble(candle.OpenTime.ToLocalTime().DateTime),
                    (double)candle.HighPrice,
                    (double)candle.LowPrice,
                    (double)candle.OpenPrice,
                    (double)candle.ClosePrice
                )));

            CandlesChart.ResetAllAxes();
            CandlesChart.InvalidatePlot(true);
        }
        catch (HttpRequestException e)
        {
            MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private bool CanGetCandleSeries() => !string.IsNullOrWhiteSpace(CandlesPair) &&
                                         CandlesLimit >= 1;
}

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

public record TradeViewModel(
    int Number,
    string Pair,
    decimal Amount,
    decimal Price,
    string Side,
    string Time,
    string Id);