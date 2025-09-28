using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class InvitesViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public InvitesViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    [ObservableProperty] private string _code = "";

    public Task LoadAsync(string tripId) { TripId = tripId; Code = ""; return Task.CompletedTask; }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var inv = await _client.CreateInviteAsync(TripId, expiresInMinutes: 120, maxUses: 10);
        if (inv is not null) Code = inv.Value.code;
    }

    [RelayCommand]
    private async Task JoinAsync()
    {
        if (!string.IsNullOrWhiteSpace(Code))
        {
            await _client.JoinByCodeAsync(Code);
        }
    }
}