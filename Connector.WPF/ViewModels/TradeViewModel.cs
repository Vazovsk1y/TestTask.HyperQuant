namespace Connector.WPF.ViewModels;

public record TradeViewModel(
    int Number,
    string Pair,
    decimal Amount,
    decimal Price,
    string Side,
    string Time,
    string Id);