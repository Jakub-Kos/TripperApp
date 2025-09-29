using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

public enum JoinMode { ClaimPlaceholder, JoinAsMe }

/// <summary>
/// Dialog ViewModel for joining a trip: claim a placeholder with a claim code or join via invite code.
/// </summary>
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
            Busy = true; Status = "Processing…";

            // If user entered a claim code, try direct claim
            if (!string.IsNullOrWhiteSpace(ClaimCode))
            {
                var dn = string.IsNullOrWhiteSpace(ClaimDisplayName) ? null : ClaimDisplayName;
                await _client.ClaimPlaceholderAsync(ClaimCode, dn);
                Status = "Placeholder claimed.";
                Close?.Invoke(true);
                return;
            }

            // Otherwise treat as invite code: resolve and open selection dialog
            var resolved = await _client.ResolveInviteAsync(InviteCode);
            if (resolved is null)
            {
                Status = "Invalid or expired invite code.";
                return;
            }

            var tripId = resolved.Value.tripId;
            // Open SelectPlaceholderDialog to pick one
            var vm = new SelectPlaceholderViewModel(_client, tripId);
            var dlg = new TripPlanner.Wpf.Views.SelectPlaceholderDialog(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            Busy = false; // release busy before showing dialog
            var ok = dlg.ShowDialog() == true;
            if (ok)
            {
                Status = "Placeholder claimed.";
                Close?.Invoke(true);
            }
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
