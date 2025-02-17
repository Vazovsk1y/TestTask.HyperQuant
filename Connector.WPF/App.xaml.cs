using System.Windows;
using Connector.WPF.ViewModels;
using Connector.WPF.Views;

namespace Connector.WPF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        var viewModel = new MainWindowViewModel();
        window.DataContext = viewModel;
        
        window.Show();
    }
}