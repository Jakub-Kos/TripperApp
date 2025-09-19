using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripPlanner.Client;
using TripPlanner.Wpf.Auth;
using TripPlanner.Wpf.ViewModels;
using TripPlanner.Wpf.Views;

namespace TripPlanner.Wpf;

public partial class App : Application
{
    public static IHost Host { get; private set; } = default!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Make exceptions visible instead of silent exit
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            MessageBox.Show(ex.ExceptionObject?.ToString() ?? "(null)", "Fatal (AppDomain)", MessageBoxButton.OK, MessageBoxImage.Error);
        DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "UI Thread Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true; // keep app alive
        };
        TaskScheduler.UnobservedTaskException += (s, ex) =>
            MessageBox.Show(ex.Exception.ToString(), "Task Exception", MessageBoxButton.OK, MessageBoxImage.Error);

        try
        {
            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg => cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true))
                .ConfigureServices((ctx, services) =>
                {
                    // Auth & token persistence
                    services.AddSingleton<TokenStore>();
                    services.AddSingleton<WpfAuthState>();
                    services.AddSingleton<IAuthState>(sp => sp.GetRequiredService<WpfAuthState>());

                    // Typed API clients (base address from appsettings.json)
                    services.AddTripPlannerClient(opts =>
                    {
                        var baseAddr = ctx.Configuration.GetSection("TripPlanner")["BaseAddress"] ?? "http://localhost:5162";
                        opts.BaseAddress = baseAddr;
                    });

                    // Destinations API + VM
                    services.AddDestinationsClient();
                    services.AddSingleton<DestinationsViewModel>();

                    // VMs
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<LoginViewModel>();

                    // Windows
                    services.AddSingleton<MainWindow>(sp =>
                    {
                        var vm = sp.GetRequiredService<MainViewModel>();
                        return new MainWindow { DataContext = vm };
                    });
                    services.AddTransient<LoginWindow>(sp =>
                    {
                        var vm = sp.GetRequiredService<LoginViewModel>();
                        return new LoginWindow(vm);
                    });
                })
                .Build();

            await Host.StartAsync();

            // Prepare MainWindow early (good for owner relationships)
            var mainWindow = Host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;

            // Try silent refresh (if a refresh token is on disk)
            var state = Host.Services.GetRequiredService<WpfAuthState>();
            state.LoadRefreshTokenFromDisk();
            if (!string.IsNullOrWhiteSpace(state.RefreshToken))
            {
                try
                {
                    var auth = Host.Services.GetRequiredService<AuthClient>();
                    var r = await auth.RefreshAsync(state.RefreshToken!);
                    state.SetTokens(r.AccessToken, r.ExpiresInSeconds, r.RefreshToken);
                }
                catch
                {
                    // ignore; we'll prompt login
                }
            }

            // If still unauthenticated, show login dialog before main window
            if (string.IsNullOrWhiteSpace(state.AccessToken))
            {
                var login = Host.Services.GetRequiredService<LoginWindow>();
                var ok = (login.ShowDialog() == true);
                if (!ok)
                {
                    Shutdown(); // user cancelled
                    return;
                }
            }

            // IMPORTANT: initialize the main VM AFTER auth so API calls carry the token
            var mainVm = Host.Services.GetRequiredService<MainViewModel>();
            await mainVm.InitializeAsync();

            // Show app
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (Host is not null)
        {
            await Host.StopAsync();
            Host.Dispose();
        }
        base.OnExit(e);
    }
}
