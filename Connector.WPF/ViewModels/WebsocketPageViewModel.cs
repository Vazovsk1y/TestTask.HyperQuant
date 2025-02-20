using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Connector.Contracts;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Connector.WPF.ViewModels;

public partial class WebsocketPageViewModel(ITestConnector connector) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubscribeTradesCommand))]
    private string? _tradesPair;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubscribeCandlesCommand))]
    private string? _candlesPair;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubscribeTradesCommand))]
    private uint _tradesLimit = 100;
    
    [ObservableProperty]
    private KeyValuePair<string, int> _candlesTimeFrame = RestPageViewModel.AvailableTimeFrames.First();

    private string? _currentTradePair;
    private string? _currentCandlesPair;
    
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
    
    public ObservableCollection<TradeViewModel> Trades { get; } = [];
    
    [RelayCommand(CanExecute = nameof(CanSubscribeTrades))]
    private async Task SubscribeTrades()
    {
        UnsubscribeTrades();
        
        connector.NewBuyTrade += OnTradeReceived;
        connector.NewSellTrade += OnTradeReceived;
        
        await connector.SubscribeTrades(TradesPair!, (int)TradesLimit);
        _currentTradePair = TradesPair;
    }

    [RelayCommand]
    private void UnsubscribeTrades()
    {
        if (string.IsNullOrWhiteSpace(_currentTradePair))
        {
            return;
        }
        
        connector.UnsubscribeTrades(_currentTradePair);
        connector.NewBuyTrade -= OnTradeReceived;
        connector.NewSellTrade -= OnTradeReceived;
        Trades.Clear();
        _currentTradePair = null;
    }
    
    private bool CanSubscribeTrades() => !string.IsNullOrWhiteSpace(TradesPair) &&
                                         TradesLimit >= 1;

    [RelayCommand(CanExecute = nameof(CanSubscribeCandles))]
    private async Task SubscribeCandles()
    {
        UnsubscribeCandles();

        connector.CandleSeriesProcessing += OnCandleReceived;
        await connector.SubscribeCandles(CandlesPair!, CandlesTimeFrame.Value);
        _currentCandlesPair = CandlesPair;
    }
    
    private bool CanSubscribeCandles() => !string.IsNullOrWhiteSpace(CandlesPair);
    
    [RelayCommand]
    private void UnsubscribeCandles()
    {
        if (string.IsNullOrWhiteSpace(_currentCandlesPair))
        {
            return;
        }
        
        connector.UnsubscribeCandles(_currentCandlesPair);
        connector.CandleSeriesProcessing -= OnCandleReceived;
        CandlesChart.Series.OfType<CandleStickSeries>().First().Items.Clear();
        CandlesChart.ResetAllAxes();
        CandlesChart.InvalidatePlot(true);
        _currentCandlesPair = null;
    }
    
    private void OnTradeReceived(Trade trade)
    {
        var vm = new TradeViewModel(
            Trades.Count + 1,
            trade.Pair,
            trade.Amount,
            trade.Price,
            trade.Side,
            trade.Time.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),
            trade.Id);

        Application.Current.Dispatcher.Invoke(() =>
        {
            Trades.Add(vm);
        });
    }

    private void OnCandleReceived(Candle candle)
    {
        var series = CandlesChart.Series.OfType<CandleStickSeries>().First();
        var item = new HighLowItem(
            DateTimeAxis.ToDouble(candle.OpenTime.ToLocalTime().DateTime),
            (double)candle.HighPrice,
            (double)candle.LowPrice,
            (double)candle.OpenPrice,
            (double)candle.ClosePrice
        );

        const double epsilon = 1e-6;
        var lastIndex = series.Items.FindIndex(c => Math.Abs(c.X - item.X) < epsilon);

        if (lastIndex >= 0)
        {
            series.Items[lastIndex] = item;
        }
        else
        {
            if (series.Items.Count == 0 || item.X > series.Items.Last().X + epsilon)
            {
                series.Items.Add(item);
            }
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            CandlesChart.InvalidatePlot(true);
        });
    }
}