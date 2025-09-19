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

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            var ok = await vm.SignInAsync(PasswordBox.Password);
            if (ok) { DialogResult = true; Close(); }
        }
    }
    
    private async void OnRegister(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            try
            {
                await vm.RegisterAsync(PasswordBox.Password);
                MessageBox.Show("Registered. You can now sign in.", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Register failed.", "Error");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
}
