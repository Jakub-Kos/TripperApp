using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class DatesViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public DatesViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";

    // Range (organizer sets this)
    [ObservableProperty] private DateTime? _rangeStart;
    [ObservableProperty] private DateTime? _rangeEnd;

    // Voting as...
    public ObservableCollection<VoterOption> VoterOptions { get; } = new();
    [ObservableProperty] private VoterOption? _selectedVoter;

    // Calendar
    public ObservableCollection<DayCell> CalendarDays { get; } = new();

    // Raw availability (date -> participant ids who voted)
    public ObservableCollection<DateAvailability> Availability { get; } = new();

    // Term proposals
    public ObservableCollection<TermRow> Terms { get; } = new();
    [ObservableProperty] private DateTime? _proposedStart;
    [ObservableProperty] private DateTime? _proposedEnd;

    // Internals
    [ObservableProperty] private string _myParticipantId = "";
    [ObservableProperty] private bool _isOrganizerMe;
    [ObservableProperty] private int _participantCount;

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        await RefreshParticipantsAsync();            // sets IsOrganizerMe, MyParticipantId, ParticipantCount and VoterOptions
        await RefreshRangeAndAvailability();         // fills Availability
        await RefreshTerms();                        // right-side list
        RebuildCalendar();                           // from Range or availability
    }

    private async Task RefreshParticipantsAsync()
    {
        VoterOptions.Clear();
        MyParticipantId = "";
        IsOrganizerMe = false;

        var participants = await _client.ListParticipantsAsync(TripId);
        if (participants is null) { ParticipantCount = 0; return; }

        string? organizerName = null;
        foreach (var p in participants)
        {
            var pid = GetString(p, "ParticipantId");
            var displayName = GetString(p, "DisplayName");
            var isPlaceholder = GetBool(p, "IsPlaceholder");
            var isOrganizer = GetBool(p, "IsOrganizer");
            var isMe = GetBool(p, "IsMe");
            var username = GetNullableString(p, "Username");

            if (isMe) MyParticipantId = pid;
            if (isMe && isOrganizer) IsOrganizerMe = true;
            if (isOrganizer) organizerName = username ?? displayName;
        }

        ParticipantCount = participants.Count();

        // Build voter options: self always. Placeholders only if organizer.
        var selfLabel = "Me";
        VoterOptions.Add(new VoterOption(null, selfLabel, true));

        foreach (var p in participants)
        {
            var pid = GetString(p, "ParticipantId");
            var displayName = GetString(p, "DisplayName");
            var isPlaceholder = GetBool(p, "IsPlaceholder");
            if (isPlaceholder)
                VoterOptions.Add(new VoterOption(pid, $"Placeholder: {displayName}", false));
        }

        SelectedVoter = VoterOptions.FirstOrDefault();
    }

    private async Task RefreshRangeAndAvailability()
    {
        Availability.Clear();
        var raw = await _client.ListDateVotesAsync(TripId); // [{ date, participantIds }]
        if (raw is null) return;
        foreach (var r in raw)
            Availability.Add(new DateAvailability(r.Date, r.ParticipantIds.ToArray()));
    }

    private void RebuildCalendar()
    {
        CalendarDays.Clear();

        // Determine start/end to render
        DateTime start, end;
        if (RangeStart is not null && RangeEnd is not null && RangeStart <= RangeEnd)
        {
            start = RangeStart.Value.Date;
            end = RangeEnd.Value.Date;
        }
        else if (Availability.Count > 0)
        {
            var dates = Availability.Select(a => DateTime.ParseExact(a.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)).OrderBy(d => d).ToArray();
            start = dates.First();
            end = dates.Last();
        }
        else
        {
            var today = DateTime.Today;
            start = new DateTime(today.Year, today.Month, 1);
            end = start.AddMonths(1).AddDays(-1);
        }

        // Align to weeks (start Monday)
        int shift = ((int)start.DayOfWeek + 6) % 7; // Monday=0
        var gridStart = start.AddDays(-shift);

        var totalDays = (end - gridStart).Days + 1;
        var weeks = (int)Math.Ceiling(totalDays / 7.0);
        var gridEnd = gridStart.AddDays(weeks * 7 - 1);

        // Index availability
        var map = Availability.ToDictionary(a => a.Date, a => a.ParticipantIds, StringComparer.Ordinal);
        string selectedPid = SelectedVoter?.ParticipantId ?? MyParticipantId;

        for (var d = gridStart; d <= gridEnd; d = d.AddDays(1))
        {
            var key = d.ToString("yyyy-MM-dd");
            map.TryGetValue(key, out var voters);
            int votes = voters?.Length ?? 0;
            double pct = (ParticipantCount > 0) ? votes / (double)ParticipantCount : 0.0;
            bool inRange = d >= start && d <= end;
            bool isMine = voters != null && !string.IsNullOrEmpty(selectedPid) && voters.Contains(selectedPid);

            CalendarDays.Add(new DayCell(d, inRange, votes, pct, isMine));
        }
    }

    partial void OnSelectedVoterChanged(VoterOption? value)
    {
        // Mark mine overlay per selected voter
        RebuildCalendar();
    }

    // === Range ===
    [RelayCommand]
    private async Task SaveRangeAsync()
    {
        if (RangeStart is null || RangeEnd is null) return;
        var ok = await _client.SetDateRangeAsync(TripId, RangeStart!.Value.ToString("yyyy-MM-dd"), RangeEnd!.Value.ToString("yyyy-MM-dd"));
        if (ok)
        {
            await RefreshRangeAndAvailability();
            RebuildCalendar();
        }
    }

    // === Calendar toggle ===
    [RelayCommand]
    private async Task ToggleDayAsync(DayCell? cell)
    {
        if (cell is null || !cell.InRange) return;
        var dateStr = cell.Date.ToString("yyyy-MM-dd");

        bool ok = false;
        if (string.IsNullOrWhiteSpace(SelectedVoter?.ParticipantId))
        {
            // act as me
            ok = cell.IsSelectedVoterVoted
                ? await _client.UnvoteOnDateAsync(TripId, dateStr)
                : await _client.VoteOnDateAsync(TripId, dateStr);
        }
        else
        {
            // act as placeholder - only organizer can proxy vote
            if (!IsOrganizerMe)
            {
                return; // non-organizers cannot proxy vote; keep UI change disabled
            }
            ok = cell.IsSelectedVoterVoted
                ? await _client.UnvoteOnDateProxyAsync(TripId, dateStr, SelectedVoter.ParticipantId!)
                : await _client.VoteOnDateProxyAsync(TripId, dateStr, SelectedVoter.ParticipantId!);
        }

        if (ok)
        {
            await RefreshRangeAndAvailability();
            RebuildCalendar();
        }
    }

    // === Terms ===

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

    [RelayCommand]
    private async Task VoteTermAsync(TermRow row)
    {
        if (row is null) return;
        bool ok;
        if (!string.IsNullOrWhiteSpace(SelectedVoter?.ParticipantId))
        {
            // try proxy if client supports it
            ok = await InvokeIfExistsBoolAsync(_client, "VoteTermProxyAsync", TripId, row.TermId, SelectedVoter!.ParticipantId!)
                 ?? await _client.VoteTermAsync(TripId, row.TermId);
        }
        else
        {
            ok = await _client.VoteTermAsync(TripId, row.TermId);
        }
        if (ok) await RefreshTerms();
    }

    [RelayCommand]
    private async Task UnvoteTermAsync(TermRow row)
    {
        if (row is null) return;
        bool ok;
        if (!string.IsNullOrWhiteSpace(SelectedVoter?.ParticipantId))
        {
            ok = await InvokeIfExistsBoolAsync(_client, "UnvoteTermProxyAsync", TripId, row.TermId, SelectedVoter!.ParticipantId!)
                 ?? await _client.UnvoteTermAsync(TripId, row.TermId);
        }
        else
        {
            ok = await _client.UnvoteTermAsync(TripId, row.TermId);
        }
        if (ok) await RefreshTerms();
    }

    [RelayCommand]
    private async Task ChooseTermAsync(TermRow row)
    {
        if (row is null) return;
        // Only the organizer can choose a term
        if (!IsOrganizerMe)
        {
            return;
        }
        var ok = await _client.ChooseTermAsync(TripId, row.TermId);
        if (ok) await RefreshTerms();
    }

    [RelayCommand]
    private async Task DeleteTermAsync(TermRow row)
    {
        if (row is null) return;
        var ok = await _client.DeleteTermAsync(TripId, row.TermId);
        if (ok) Terms.Remove(row);
    }

    private async Task RefreshTerms()
    {
        Terms.Clear();
        var list = await _client.ListTermsAsync(TripId);
        if (list is null) return;
        foreach (var t in list) Terms.Add(new TermRow(t.TermId, t.Start, t.End, t.Votes, t.IsChosen));
    }

    // ---- reflection helpers ----
    private static string GetString(object obj, string name)
        => obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
              ?.GetValue(obj)?.ToString() ?? string.Empty;

    private static string? GetNullableString(object obj, string name)
        => obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
              ?.GetValue(obj)?.ToString();

    private static bool GetBool(object obj, string name)
        => (obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
              ?.GetValue(obj) as bool?) ?? false;

    private static async Task<bool?> InvokeIfExistsBoolAsync(object target, string methodName, params object[] args)
    {
        var mi = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (mi is null) return null;
        var result = mi.Invoke(target, args);
        if (result is Task<bool> tb) return await tb;
        if (result is Task t) { await t; return true; }
        if (result is bool b) return b;
        return null;
    }
}

// ===== supporting records =====
public sealed record DateAvailability(string Date, string[] ParticipantIds);
public sealed record TermRow(string TermId, string Start, string End, int Votes, bool IsChosen);



public partial class DayCell : ObservableObject
{
    public DayCell(DateTime date, bool inRange, int votes, double pct, bool isSelectedVoterVoted)
    {
        Date = date;
        InRange = inRange;
        _voteCount = votes;
        _votePercent = pct;
        _isSelectedVoterVoted = isSelectedVoterVoted;
    }

    public DateTime Date { get; }
    public bool InRange { get; }

    [ObservableProperty] private int _voteCount;
    [ObservableProperty] private double _votePercent; // 0..1
    [ObservableProperty] private bool _isSelectedVoterVoted;
}
