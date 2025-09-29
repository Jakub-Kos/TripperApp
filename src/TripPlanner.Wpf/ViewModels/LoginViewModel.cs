using System.ComponentModel;
using System.Runtime.CompilerServices;
using TripPlanner.Client;
using TripPlanner.Core.Contracts.Contracts.Common;
using TripPlanner.Wpf.Auth;

namespace TripPlanner.Wpf.ViewModels;

/// <summary>
/// Login screen ViewModel: handles sign-in and registration, storing tokens via WpfAuthState.
/// </summary>
public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly AuthClient _auth;
    private readonly WpfAuthState _state;

    public LoginViewModel(AuthClient auth, WpfAuthState state)
    {
        _auth = auth; _state = state;
    }

    private string _email = "";
    public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

    private string _error = "";
    public string Error { get => _error; set { _error = value; OnPropertyChanged(); } }

    public async Task<bool> SignInAsync(string password)
    {
        try
        {
            var email = (Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                Error = "Please enter both email and password.";
                return false;
            }

            var resp = await _auth.LoginAsync(email, password);
            if (resp is null)
            {
                Error = "Sign in failed. Server unavailable or returned an error.";
                return false;
            }
            _state.SetTokens(resp.AccessToken, resp.ExpiresInSeconds, resp.RefreshToken);
            Error = "";
            return true;
        }
        catch (Exception ex)
        {
            Error = "Sign in failed.";
            System.Diagnostics.Debug.WriteLine(ex);
            return false;
        }
    }
    public async Task<bool> RegisterAsync(string password)
    {
        try
        {
            var email = (Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                Error = "Please enter both email and password.";
                return false;
            }

            var req = new RegisterRequest(email, password, "User");
            var ok = await _auth.RegisterAsync(req);
            if (!ok)
            {
                Error = "Registration failed. Please check your details and try again.";
                return false;
            }
            Error = "";
            return true;
        }
        catch (Exception ex)
        {
            Error = "Registration failed.";
            System.Diagnostics.Debug.WriteLine(ex);
            return false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}