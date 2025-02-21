namespace Connector.WPF.ViewModels;

public class MainWindowViewModel(
    RestPageViewModel restPageViewModel, 
    WebsocketPageViewModel websocketPageViewModel)
{
    public RestPageViewModel RestPageViewModel { get; } = restPageViewModel;
    
    public WebsocketPageViewModel WebsocketPageViewModel { get; } = websocketPageViewModel;
}