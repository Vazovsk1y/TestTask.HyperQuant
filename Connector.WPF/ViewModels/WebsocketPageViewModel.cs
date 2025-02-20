using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Connector.Contracts;

namespace Connector.WPF.ViewModels;

public partial class WebsocketPageViewModel(ITestConnector connector) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubscribeTradesCommand))]
    private string? _tradesPair;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubscribeTradesCommand))]
    private uint _tradesLimit = 100;

    private string? _currentTradePair;
    
    public ObservableCollection<TradeViewModel> Trades { get; } = [];
    
    [RelayCommand(CanExecute = nameof(CanSubscribeTrades))]
    private async Task SubscribeTrades()
    {
        if (_currentTradePair == TradesPair)
        {
            return;
        }
        
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
}