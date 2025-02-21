using System.Windows;
using Connector.WPF.ViewModels;
using Connector.WPF.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Connector.WPF;

public partial class App : Application
{
    private static IServiceProvider Services { get; }
    
    static App()
    {
        var collection = new ServiceCollection();
        
        collection.AddSingleton<MainWindow>();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<RestPageViewModel>();
        collection.AddSingleton<WebsocketPageViewModel>();
        
        collection.AddConnector();
        
        Services = collection.BuildServiceProvider();
    }
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = Services.GetRequiredService<MainWindow>();
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();
        window.DataContext = viewModel;
        
        window.Show();
    }
}