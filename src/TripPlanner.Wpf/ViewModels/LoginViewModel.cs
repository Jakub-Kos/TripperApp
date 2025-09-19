using System.ComponentModel;
using System.Runtime.CompilerServices;
using TripPlanner.Client;
using TripPlanner.Core.Contracts.Contracts.Common;
using TripPlanner.Wpf.Auth;

namespace TripPlanner.Wpf.ViewModels;

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
            var resp = await _auth.LoginAsync(Email, password);
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
    public async Task RegisterAsync(string password)
    {
        var req = new RegisterRequest(Email, password, "User");
        // Reuse AuthClient: add a RegisterAsync in AuthClient
        await _auth.RegisterAsync(req);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}