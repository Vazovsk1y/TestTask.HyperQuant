namespace Connector.WPF.ViewModels;

public class MainWindowViewModel(RestPageViewModel restPageViewModel)
{
    public RestPageViewModel RestPageViewModel { get; } = restPageViewModel;
}