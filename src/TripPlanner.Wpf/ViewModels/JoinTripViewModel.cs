using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

public enum JoinMode { ClaimPlaceholder, JoinAsMe }

public sealed partial class JoinTripViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public JoinTripViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private JoinMode _mode = JoinMode.ClaimPlaceholder;

    // Claim placeholder
    [ObservableProperty] private string _claimCode = "";
    [ObservableProperty] private string _claimDisplayName = ""; // optional

    // Join as me
    [ObservableProperty] private string _inviteCode = "";

    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _status = "";

    // Dialog close callback (set by the Window code-behind)
    public Action<bool>? Close;

    [RelayCommand]
    private void SelectClaim() => Mode = JoinMode.ClaimPlaceholder;

    [RelayCommand]
    private void SelectJoin() => Mode = JoinMode.JoinAsMe;

    [RelayCommand]
    private async Task ClaimAsync()
    {
        var code = string.IsNullOrWhiteSpace(ClaimCode) ? InviteCode : ClaimCode;
        if (string.IsNullOrWhiteSpace(code)) { Status = "Enter code."; return; }
        try
        {
            Busy = true; Status = "Claiming placeholder…";
            var dn = string.IsNullOrWhiteSpace(ClaimDisplayName) ? null : ClaimDisplayName;
            await _client.ClaimPlaceholderAsync(code, dn);
            Status = "Placeholder claimed.";
            Close?.Invoke(true);
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }

    [RelayCommand]
    private async Task JoinAsync()
    {
        if (string.IsNullOrWhiteSpace(InviteCode)) { Status = "Enter invite code."; return; }
        try
        {
            Busy = true; Status = "Joining trip…";
            await _client.JoinByCodeAsync(InviteCode);
            Status = "Joined.";
            Close?.Invoke(true);
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }

    [RelayCommand]
    private void Cancel() => Close?.Invoke(false);
}
