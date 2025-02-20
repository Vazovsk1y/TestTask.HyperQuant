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
    [ObservableProperty] 
    private PlotModel? _candlesChart;
    
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
    
    [RelayCommand(CanExecute = nameof(CanGetTrades))]
    private async Task GetTradesAsync()
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
    private async Task GetCandleSeriesAsync()
    {
        try
        {
            var from = CandlesFrom.ToUniversalTime();
            var to = CandlesFrom.ToUniversalTime();

            var result = await connector.GetCandleSeriesAsync(CandlesPair!, CandlesTimeFrame.Value, from, to, CandlesLimit);

            var candleStickSeries = new CandleStickSeries()
            {
                Color = OxyColors.Black,
                IncreasingColor = OxyColors.Green,
                DecreasingColor = OxyColors.Red,
                Title = result.FirstOrDefault()?.Pair,
                TrackerFormatString = "{0}\nDateTime: {2:dd.MM.yyyy HH:mm}\nHigh: {3:0.###}\nLow: {4:0.###}\nOpen: {5:0.###}\nClose: {6:0.###}",
            };

            candleStickSeries.Items.AddRange(
                result
                    .OrderBy(e => e.OpenTime)
                    .Select(candle => new HighLowItem(
                        DateTimeAxis.ToDouble(candle.OpenTime.ToLocalTime().DateTime),
                        (double)candle.HighPrice,
                        (double)candle.LowPrice,
                        (double)candle.OpenPrice,
                        (double)candle.ClosePrice
                    )));
            
            var dateTimeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "dd.MM.yyyy HH:mm",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                IntervalType = DateTimeIntervalType.Auto,
                Title = "Time",
            };

            CandlesChart = new PlotModel
            {
                Title = "Candles Chart",
                Series = { candleStickSeries },
                Axes = { dateTimeAxis }
            };
        }
        catch (HttpRequestException e)
        {
            MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private bool CanGetCandleSeries() => !string.IsNullOrWhiteSpace(CandlesPair) &&
                                         CandlesLimit >= 1;
}

public record TradeViewModel(
    int Number,
    string Pair,
    decimal Amount,
    decimal Price,
    string Side,
    string Time,
    string Id);