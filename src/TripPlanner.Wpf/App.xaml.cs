using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
        
        
        // Global exception handlers so we SEE problems instead of silent exit
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            MessageBox.Show(ex.ExceptionObject.ToString(), "Fatal (AppDomain)", MessageBoxButton.OK, MessageBoxImage.Error);
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

                    // API clients
                    services.AddTripPlannerClient(opts =>
                    {
                        var baseAddr = ctx.Configuration.GetSection("TripPlanner")["BaseAddress"] ?? "http://localhost:5162";
                        opts.BaseAddress = baseAddr;
                    });

                    // Destinations API
                    services.AddSingleton<DestinationsViewModel>();
                    services.AddDestinationsClient();
                    
                    // MVVM
                    services.AddSingleton<LoginViewModel>();
                    services.AddSingleton<MainViewModel>();

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

            // Set the MainWindow early; it helps app lifetime and ownership
            var main = Host.Services.GetRequiredService<MainWindow>();
            MainWindow = main;

            // Try silent refresh first
            var state = Host.Services.GetRequiredService<WpfAuthState>();
            state.LoadRefreshTokenFromDisk();
            if (!string.IsNullOrEmpty(state.RefreshToken))
            {
                try
                {
                    var auth = Host.Services.GetRequiredService<AuthClient>();
                    var r = await auth.RefreshAsync(state.RefreshToken!);
                    state.SetTokens(r.AccessToken, r.ExpiresInSeconds, r.RefreshToken);
                }
                catch
                {
                    // ignore; will prompt login
                }
            }

            // If still unauthenticated, prompt login BEFORE showing main window
            if (string.IsNullOrEmpty(state.AccessToken))
            {
                var login = Host.Services.GetRequiredService<LoginWindow>();
                // Do not set Owner if you suspect MainWindow not shown yet; dialog works standalone.
                var ok = login.ShowDialog() == true;
                if (!ok)
                {
                    // user cancelled -> exit cleanly
                    Shutdown();
                    return;
                }
            }

            // Now show main window
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
