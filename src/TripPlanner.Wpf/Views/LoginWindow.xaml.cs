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
            ShowWorking("Signing in...");
            try
            {
                var ok = await vm.SignInAsync(PasswordBox.Password);
                if (ok) { DialogResult = true; Close(); }
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
            ShowWorking("Registering...");
            try
            {
                await vm.RegisterAsync(PasswordBox.Password);
                
                // Auto-login after successful registration
                ShowWorking("Logging in...");
                var loginSuccess = await vm.SignInAsync(PasswordBox.Password);
                if (loginSuccess)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Registration successful, but auto-login failed. Please sign in manually.", "Registration Complete");
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
