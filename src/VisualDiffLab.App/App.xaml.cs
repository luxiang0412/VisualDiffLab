using Microsoft.UI.Xaml;
using VisualDiffLab.App.Services;

namespace VisualDiffLab.App;

public partial class App : Application, IDisposable
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            DiagnosticsLog.Shared.Error("Unhandled UI exception", args.Exception);
        };
    }

    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
        GC.SuppressFinalize(this);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DiagnosticsLog.Shared.Info("Application start");
        StartupOptions options = StartupOptions.Parse(Environment.GetCommandLineArgs().Skip(1));
        _window = new MainWindow(options);
        _window.Activate();
    }
}
