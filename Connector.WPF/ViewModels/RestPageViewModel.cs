using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Connector.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Connector.WPF.ViewModels;

public partial class RestPageViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetTradesCommand))]
    private uint _tradesLimit = 125;
    
    [ObservableProperty]
    private IReadOnlyCollection<TradeViewModel>? _trades;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetTradesCommand))]
    private string? _tradesPair;

    [RelayCommand(CanExecute = nameof(CanGetTrades))]
    private async Task GetTradesAsync()
    {
        if (GetTradesCommand.IsRunning)
        {
            return;
        }

        using var scope = App.Services.CreateScope();
        var connector = scope.ServiceProvider.GetRequiredService<ITestConnector>();

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
                    e.Time.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), 
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
}

public record TradeViewModel(
    int Number,
    string Pair,
    decimal Amount,
    decimal Price,
    string Side,
    string Time,
    string Id);