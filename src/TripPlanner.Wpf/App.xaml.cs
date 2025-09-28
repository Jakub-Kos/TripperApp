using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripPlanner.Client;
using TripPlanner.Client.Abstractions;
using TripPlanner.Wpf.Auth;
using TripPlanner.Wpf.ViewModels;
using TripPlanner.Wpf.Views;

namespace TripPlanner.Wpf;

public partial class App : Application
{
    public static IHost Host { get; private set; } = default!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg => cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true))
            .ConfigureServices((ctx, services) =>
            {
                // Auth state & token persistence
                services.AddSingleton<TokenStore>();
                services.AddSingleton<WpfAuthState>();
                services.AddSingleton<IAuthState>(sp => sp.GetRequiredService<WpfAuthState>());

                // Core TripPlanner HTTP client
                services.AddTripPlannerClient(opts =>
                {
                    var baseAddr = ctx.Configuration.GetSection("TripPlanner")["BaseAddress"] ?? "http://localhost:5162";
                    opts.BaseAddress = baseAddr;
                });

                // If your client has typed registrations, add them; otherwise keep ITripPlannerClient only.
                // The current client exposes a single ITripPlannerClient; typed AddXClient extensions are not available in this repo.

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<DestinationsViewModel>();
                services.AddSingleton<OverviewViewModel>();
                services.AddSingleton<ParticipantsViewModel>();
                services.AddSingleton<DatesViewModel>();
                services.AddSingleton<GearViewModel>();
                services.AddSingleton<ItineraryViewModel>();
                services.AddSingleton<TransportationsViewModel>();
                services.AddSingleton<InvitesViewModel>();
                services.AddTransient<LoginViewModel>();

                // Windows
                services.AddSingleton<MainWindow>(sp =>
                {
                    var vm = sp.GetRequiredService<MainViewModel>();
                    var wnd = new MainWindow { DataContext = vm };
                    return wnd;
                });
                services.AddTransient<LoginWindow>();
            })
            .Build();

        await Host.StartAsync();

        // Attempt token refresh
        var state = Host.Services.GetRequiredService<WpfAuthState>();
        state.LoadRefreshTokenFromDisk();
        if (!string.IsNullOrWhiteSpace(state.RefreshToken))
        {
            try
            {
                var auth = Host.Services.GetRequiredService<AuthClient>();
                var r = await auth.RefreshAsync(state.RefreshToken!);
                if (r is not null)
                {
                    state.SetTokens(r.AccessToken, r.ExpiresInSeconds, r.RefreshToken);
                }
            }
            catch { /* ignore */ }
        }

        // Gate by login
        if (string.IsNullOrWhiteSpace(state.AccessToken))
        {
            // Ensure the app doesn't shutdown when closing the login dialog (before MainWindow is shown)
            var previousMode = this.ShutdownMode;
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var login = Host.Services.GetRequiredService<LoginWindow>();
            var result = login.ShowDialog();

            // Restore the shutdown mode regardless of dialog result
            this.ShutdownMode = previousMode;

            if (result != true)
            {
                Shutdown();
                return;
            }
        }

        // Initialize and show main window
        var main = Host.Services.GetRequiredService<MainWindow>();
        this.MainWindow = main; // set explicitly to control shutdown behavior
        main.Show();

        // Load data without blocking the UI startup
        var mainVm = Host.Services.GetRequiredService<MainViewModel>();
        _ = mainVm.InitializeAsync();

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
