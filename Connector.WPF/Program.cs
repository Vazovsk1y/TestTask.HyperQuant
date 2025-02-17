using System.IO;
using System.Windows;
using Serilog;

namespace Connector.WPF;

internal static class Program
{
    private static bool IsInDebug { get; set; }

    private static Mutex? _mutex;

    private const string AppId = "2ded2b5e-6d07-421a-8e5d-5b71a1225f17";

    [STAThread]
    public static void Main(string[] args)
    {
        
#if DEBUG
        IsInDebug = true;
#endif
        
        _mutex = new Mutex(true, AppId, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("App is already running.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", IsInDebug ? "Development" : "Production");
        Log.Logger = GetLoggerConfiguration().CreateLogger();

        try
        {
            App app = new();
            // app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Exception caught.");
        }
    }

    private static LoggerConfiguration GetLoggerConfiguration()
    {
        var loggerConfiguration = new LoggerConfiguration();
        if (IsInDebug)
        {
            loggerConfiguration.MinimumLevel.Debug();
            loggerConfiguration.WriteTo.Debug();
            return loggerConfiguration;
        }

        var logsDirectory = Path.Combine(Environment.CurrentDirectory, "logs");
        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        var logFileFullPath = Path.Combine(logsDirectory, "log.txt");

        loggerConfiguration.MinimumLevel.Information();
        loggerConfiguration.WriteTo.File(logFileFullPath, rollingInterval: RollingInterval.Day);
        return loggerConfiguration;
    }
}