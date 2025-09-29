using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.Common.Participants;

namespace TripPlanner.Wpf.ViewModels;

/// <summary>
/// Dialog ViewModel listing placeholders in a trip so the user can claim one.
/// </summary>
public sealed partial class SelectPlaceholderViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public SelectPlaceholderViewModel(ITripPlannerClient client, string tripId)
    {
        _client = client;
        TripId = tripId;
    }

    public string TripId { get; }

    public ObservableCollection<PlaceholderRow> Items { get; } = new();

    [ObservableProperty] private PlaceholderRow? _selected;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _status = string.Empty;

    public Action<bool>? Close;

    public async Task LoadAsync()
    {
        try
        {
            Busy = true; Status = "Loading placeholders...";
            Items.Clear();
            var list = await _client.ListParticipantsAsync(TripId);
            foreach (var p in list ?? Array.Empty<ParticipantInfoDto>())
            {
                if (p.IsPlaceholder)
                    Items.Add(new PlaceholderRow(p.ParticipantId!, p.DisplayName ?? "Placeholder"));
            }
            Status = Items.Count == 0 ? "No placeholders available to claim." : $"{Items.Count} placeholder(s) available.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally { Busy = false; }
    }

    public bool CanClaim => Selected != null;

    partial void OnSelectedChanged(PlaceholderRow? value) => OnPropertyChanged(nameof(CanClaim));

    [RelayCommand]
    private async Task ClaimAsync()
    {
        if (Selected is null) { Status = "Select a placeholder."; return; }
        try
        {
            Busy = true; Status = "Claiming...";
            var ok = await _client.ClaimPlaceholderInTripAsync(TripId, Selected.ParticipantId);
            if (!ok) { Status = "Failed to claim placeholder."; return; }
            Status = "Claimed.";
            Close?.Invoke(true);
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }

    [RelayCommand]
    private void Cancel() => Close?.Invoke(false);
}

public sealed record PlaceholderRow(string ParticipantId, string DisplayName);
