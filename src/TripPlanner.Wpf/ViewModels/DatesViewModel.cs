using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class DatesViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public DatesViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    [ObservableProperty] private DateTime? _rangeStart;
    [ObservableProperty] private DateTime? _rangeEnd;

    public ObservableCollection<DateAvailability> Availability { get; } = new();
    public ObservableCollection<TermRow> Terms { get; } = new();

    [ObservableProperty] private DateTime? _voteDate;
    [ObservableProperty] private string _proxyParticipantId = "";

    [ObservableProperty] private DateTime? _proposedStart;
    [ObservableProperty] private DateTime? _proposedEnd;

    [RelayCommand]
    private async Task ProposeTermAsync()
    {
        if (ProposedStart is null || ProposedEnd is null) return;
        var ok = await _client.ProposeTermAsync(TripId, ProposedStart!.Value.ToString("yyyy-MM-dd"), ProposedEnd!.Value.ToString("yyyy-MM-dd"));
        if (ok)
        {
            ProposedStart = ProposedEnd = null;
            await RefreshTerms();
        }
    }
    
    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        await RefreshRangeAndAvailability();
        await RefreshTerms();
    }

    private async Task RefreshRangeAndAvailability()
    {
        Availability.Clear();
        var raw = await _client.ListDateVotesAsync(TripId); // [{ date, isChosen, participantIds }]
        if (raw is null) return;
        foreach (var r in raw)
            Availability.Add(new DateAvailability(r.Date, r.ParticipantIds.ToArray()));
    }

    [RelayCommand]
    private async Task SaveRangeAsync()
    {
        if (RangeStart is null || RangeEnd is null) return;
        var ok = await _client.SetDateRangeAsync(TripId, RangeStart!.Value.ToString("yyyy-MM-dd"), RangeEnd!.Value.ToString("yyyy-MM-dd"));
        if (ok) await RefreshRangeAndAvailability();
    }

    [RelayCommand]
    private async Task VoteSelfAsync()
    {
        if (VoteDate is null) return;
        var ok = await _client.VoteOnDateAsync(TripId, VoteDate!.Value.ToString("yyyy-MM-dd"));
        if (ok) await RefreshRangeAndAvailability();
    }

    [RelayCommand]
    private async Task UnvoteSelfAsync()
    {
        if (VoteDate is null) return;
        var ok = await _client.UnvoteOnDateAsync(TripId, VoteDate!.Value.ToString("yyyy-MM-dd"));
        if (ok) await RefreshRangeAndAvailability();
    }

    [RelayCommand]
    private async Task VoteProxyAsync()
    {
        if (VoteDate is null || string.IsNullOrWhiteSpace(ProxyParticipantId)) return;
        var ok = await _client.VoteOnDateProxyAsync(TripId, VoteDate!.Value.ToString("yyyy-MM-dd"), ProxyParticipantId);
        if (ok) await RefreshRangeAndAvailability();
    }

    [RelayCommand]
    private async Task UnvoteProxyAsync()
    {
        if (VoteDate is null || string.IsNullOrWhiteSpace(ProxyParticipantId)) return;
        var ok = await _client.UnvoteOnDateProxyAsync(TripId, VoteDate!.Value.ToString("yyyy-MM-dd"), ProxyParticipantId);
        if (ok) await RefreshRangeAndAvailability();
    }

    private async Task RefreshTerms()
    {
        Terms.Clear();
        var list = await _client.ListTermsAsync(TripId);
        if (list is null) return;
        foreach (var t in list) Terms.Add(new TermRow(t.TermId, t.Start, t.End, t.Votes, t.IsChosen));
    }

    private async Task ProposeTermWithParamsAsync(DateTime start, DateTime end)
    {
        await _client.ProposeTermAsync(TripId, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
        await RefreshTerms();
    }

    [RelayCommand] private async Task VoteTermAsync(TermRow row) { var ok = await _client.VoteTermAsync(TripId, row.TermId); if (ok) await RefreshTerms(); }
    [RelayCommand] private async Task UnvoteTermAsync(TermRow row) { var ok = await _client.UnvoteTermAsync(TripId, row.TermId); if (ok) await RefreshTerms(); }
    [RelayCommand] private async Task ChooseTermAsync(TermRow row) { var ok = await _client.ChooseTermAsync(TripId, row.TermId); if (ok) await RefreshTerms(); }
    [RelayCommand] private async Task DeleteTermAsync(TermRow row) { var ok = await _client.DeleteTermAsync(TripId, row.TermId); if (ok) Terms.Remove(row); }
}

public sealed record DateAvailability(string Date, string[] ParticipantIds);
public sealed record TermRow(string TermId, string Start, string End, int Votes, bool IsChosen);
