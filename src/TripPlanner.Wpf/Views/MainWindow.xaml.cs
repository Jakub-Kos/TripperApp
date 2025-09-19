using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Wpf.Auth;
using TripPlanner.Wpf.Views; 
using TripPlanner.Client;
using TripPlanner.Wpf.ViewModels;

namespace TripPlanner.Wpf.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();
        
        private async void OnSignOut(object sender, RoutedEventArgs e)
        {
            var sp = App.Host.Services;
            var state = sp.GetRequiredService<WpfAuthState>();
            var store = sp.GetRequiredService<TokenStore>();
            var auth = sp.GetRequiredService<AuthClient>();

            try
            {
                if (!string.IsNullOrEmpty(state.RefreshToken))
                    await auth.LogoutAsync(state.RefreshToken); // server-side revoke (best-effort)
            }
            catch { /* ignore network errors on logout */ }

            store.Clear();   // delete token file
            state.Clear();   // clear in-memory tokens

            var login = sp.GetRequiredService<LoginWindow>();
            var ok = login.ShowDialog() == true;
            if (!ok)
            {
                Application.Current.Shutdown(); // user cancelled login
                return;
            }
            
            // // Optionally reload data after re-login
            // if (DataContext is MainViewModel vm)
            //     await vm.LoadTripsAsync();
        }
    }
}
