using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripPlanner.Client;
using TripPlanner.Wpf.ViewModels;
using ViewsMainWindow = TripPlanner.Wpf.Views.MainWindow;

namespace TripPlanner.Wpf;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        // (Optional) surface any silent XAML/runtime exceptions
        this.DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "Unhandled", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                // Typed API client
                services.AddTripPlannerClient(opts =>
                {
                    opts.BaseAddress = ctx.Configuration.GetSection("TripPlanner")["BaseAddress"]
                                       ?? "http://localhost:5162";
                });

                // MVVM + Window (note: register the *Views* MainWindow here)
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<ViewsMainWindow>(sp =>
                {
                    var vm = sp.GetRequiredService<MainViewModel>();
                    return new ViewsMainWindow { DataContext = vm };
                });
            })
            .Build();

        _host.Start();

        // Resolve & show the window
        var window = _host.Services.GetRequiredService<ViewsMainWindow>();
        Current.MainWindow = window;                    // helps ShutdownMode
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
