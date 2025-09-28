using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.Common.Participants;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class ParticipantsViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public ParticipantsViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    public ObservableCollection<ParticipantRow> Items { get; } = new();

    [ObservableProperty] private string _newPlaceholderName = "";
    [ObservableProperty] private string _inviteCode = "";
    [ObservableProperty] private string _claimCode = "";

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        Items.Clear();
        var ps = await _client.ListParticipantsAsync(tripId);
        if (ps is null) return;
        foreach (var p in ps) Items.Add(new(p.ParticipantId, p.DisplayName));
    }

    [RelayCommand]
    private async Task AddPlaceholderAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPlaceholderName)) return;
        await _client.CreatePlaceholderAsync(TripId, NewPlaceholderName);
        await LoadAsync(TripId);
        NewPlaceholderName = "";
    }

    [RelayCommand]
    private async Task RenameAsync(ParticipantRow row)
    {
        await _client.UpdateParticipantDisplayNameAsync(TripId, row.ParticipantId, row.DisplayName);
    }

    [RelayCommand]
    private async Task RemoveAsync(ParticipantRow row)
    {
        await _client.DeleteParticipantAsync(TripId, row.ParticipantId);
        Items.Remove(row);
    }

    [RelayCommand]
    private async Task IssueClaimCodeAsync(ParticipantRow row)
    {
        var info = await _client.IssueClaimCodeAsync(TripId, row.ParticipantId, 30);
        if (info is not null) ClaimCode = info.Value.code;
    }

    [RelayCommand]
    private async Task ClaimPlaceholderAsync()
    {
        if (string.IsNullOrWhiteSpace(ClaimCode)) return;
        await _client.ClaimPlaceholderAsync(ClaimCode, null);
        ClaimCode = "";
        await LoadAsync(TripId);
    }

    [RelayCommand]
    private async Task CreateInviteCodeAsync()
    {
        var inv = await _client.CreateInviteAsync(TripId, expiresInMinutes: 120, maxUses: 10);
        if (inv is not null) InviteCode = inv.Value.code;
    }

    [RelayCommand]
    private async Task JoinByCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(InviteCode)) return;
        await _client.JoinByCodeAsync(InviteCode);
        InviteCode = "";
    }
}

public sealed partial class ParticipantRow : ObservableObject
{
    public ParticipantRow(string pid, string displayName) { ParticipantId = pid; _displayName = displayName; }
    public string ParticipantId { get; }
    [ObservableProperty] private string _displayName;
}
