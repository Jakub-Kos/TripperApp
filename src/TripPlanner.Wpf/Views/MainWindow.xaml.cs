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
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void NewTripTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (vm.NewTripName == MainViewModel.NewTripPlaceholder)
                {
                    vm.NewTripName = string.Empty;
                }
            }
        }

        private void NewTripTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (string.IsNullOrWhiteSpace(vm.NewTripName))
                {
                    vm.NewTripName = MainViewModel.NewTripPlaceholder;
                }
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure DataContext is set and data is initialized even if window was not constructed via DI factory
            if (DataContext is not MainViewModel vm)
            {
                var sp = App.Host.Services;
                vm = sp.GetRequiredService<MainViewModel>();
                DataContext = vm;
            }

            // Initialize trips and tabs if not yet done
            await vm.InitializeAsync();
        }
        
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
            
            // Reload data after re-login
            if (DataContext is MainViewModel vm2)
                await vm2.InitializeAsync();
        }
    }
}
