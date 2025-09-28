using System.Windows;
using TripPlanner.Wpf.ViewModels;

namespace TripPlanner.Wpf.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void ShowWorking(string message = "Processing...")
    {
        WorkingText.Text = message;
        WorkingOverlay.Visibility = Visibility.Visible;
        RegisterButton.IsEnabled = false;
        SignInButton.IsEnabled = false;
    }

    private void HideWorking()
    {
        WorkingOverlay.Visibility = Visibility.Collapsed;
        RegisterButton.IsEnabled = true;
        SignInButton.IsEnabled = true;
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            var pwd = PasswordBox.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(vm.Email) || string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("Please enter both email and password.", "Missing information");
                return;
            }

            ShowWorking("Signing in...");
            try
            {
                var ok = await vm.SignInAsync(pwd);
                if (ok) { DialogResult = true; Close(); }
                else if (!string.IsNullOrWhiteSpace(vm.Error))
                {
                    MessageBox.Show(vm.Error, "Sign in failed");
                }
            }
            finally
            {
                HideWorking();
            }
        }
    }
    
    private async void OnRegister(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            var pwd = PasswordBox.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(vm.Email) || string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("Please enter both email and password.", "Missing information");
                return;
            }

            ShowWorking("Registering...");
            try
            {
                var registered = await vm.RegisterAsync(pwd);
                if (!registered)
                {
                    MessageBox.Show(vm.Error?.Length > 0 ? vm.Error : "Registration failed. Please try again.", "Registration Error");
                    return;
                }
                
                // Auto-login after successful registration
                ShowWorking("Logging in...");
                var loginSuccess = await vm.SignInAsync(pwd);
                if (loginSuccess)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(!string.IsNullOrWhiteSpace(vm.Error) ? vm.Error : "Registration successful, but auto-login failed. Please sign in manually.", "Registration Complete");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Registration failed. Please try again.", "Error");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                HideWorking();
            }
        }
    }
}
