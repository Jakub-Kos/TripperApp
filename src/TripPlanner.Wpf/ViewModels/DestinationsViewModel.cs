using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class DestinationsViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public DestinationsViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    public ObservableCollection<VoterOption> VoterOptions { get; } = new();
    [ObservableProperty] private VoterOption? _selectedVoter;

    public ObservableCollection<DestinationCard> Items { get; } = new();

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        await RefreshVotersAsync();
        await RefreshDestinationsAsync();
    }

    private async Task RefreshVotersAsync()
    {
        VoterOptions.Clear();
        // Everyone can vote as placeholders – list participants and pick placeholders.
        var parts = await _client.ListParticipantsAsync(TripId);
        VoterOptions.Add(new VoterOption(null, "Me", true)); // self
        if (parts is not null)
        {
            foreach (var p in parts)
            {
                var isPlaceholder = GetBool(p, "IsPlaceholder");
                if (!isPlaceholder) continue;
                VoterOptions.Add(new VoterOption(
                    GetString(p, "ParticipantId"),
                    $"Placeholder: {GetString(p, "DisplayName")}",
                    false));
            }
        }
        SelectedVoter = VoterOptions.FirstOrDefault();
    }

    public async Task RefreshDestinationsAsync()
    {
        Items.Clear();

        // GET /api/v1/trips/{tripId}/destinations
        var list = await _client.GetDestinationsAsync(TripId); // returns DestinationProposalDto[]
        if (list is null) return;

        int maxVotes = 0;

        foreach (var d in list)
        {
            var id = GetString(d, "DestinationId");
            var title = GetString(d, "Title");
            var desc = GetNullableString(d, "Description") ?? "";
            var votes = GetInt(d, "Votes");
            var imageUrls = GetStringArray(d, "ImageUrls") ?? Array.Empty<string>();

            var card = new DestinationCard
            {
                DestinationId = id,
                Name = title,
                Description = desc,
                VoteCount = votes,
                ImageUrl = imageUrls.FirstOrDefault()
            };

            Items.Add(card);
            maxVotes = Math.Max(maxVotes, votes);
        }

        // mark most-voted
        foreach (var it in Items)
            it.IsMostVoted = it.VoteCount == maxVotes && maxVotes > 0;

        // fetch who voted for each (detail endpoint includes participantIds)
        foreach (var it in Items)
        {
            try
            {
                var detail = await _client.GetDestinationAsync(TripId, it.DestinationId);
                it.VoterParticipantIds = GetStringArray(detail, "ParticipantIds") ?? Array.Empty<string>();
            }
            catch { /* non-fatal; leave empty */ }
        }

        RecomputeSelectedFlags();

        // trailing "+" add card
        Items.Add(DestinationCard.CreateAddCard());
    }

    partial void OnSelectedVoterChanged(VoterOption? oldValue) => RecomputeSelectedFlags();

    private void RecomputeSelectedFlags()
    {
        var pid = SelectedVoter?.ParticipantId;
        foreach (var it in Items)
        {
            if (it.IsAddNew) continue;
            it.IsSelectedVoterVoted = !string.IsNullOrWhiteSpace(pid) && it.VoterParticipantIds.Contains(pid);
        }
    }

    // --- Commands ---

    [RelayCommand]
    private async Task ToggleVoteAsync(DestinationCard? card)
    {
        if (card is null || card.IsAddNew) return;

        if (SelectedVoter?.ParticipantId is null)
        {
            if (card.IsSelectedVoterVoted)
            {
                // DELETE self vote
                await _client.UnvoteDestinationAsync(TripId, card.DestinationId);
            }
            else
            {
                // POST self vote
                await _client.VoteDestinationAsync(TripId, card.DestinationId, new VoteDestinationRequest(Guid.Empty));
            }
        }
        else
        {
            var pid = SelectedVoter.ParticipantId;
            if (card.IsSelectedVoterVoted)
            {
                await _client.ProxyUnvoteDestinationAsync(TripId, card.DestinationId, pid);
            }
            else
            {
                await _client.ProxyVoteDestinationAsync(TripId, card.DestinationId, pid);
            }
        }

        await RefreshDestinationsAsync();
    }

    [RelayCommand]
    private async Task AddNewAsync()
    {
        var vm = new DestinationEditViewModel(_client)
        {
            Mode = DestinationEditMode.Create,
            TripId = TripId
        };
        var dlg = new TripPlanner.Wpf.Views.DestinationEditDialog(vm) { Owner = Application.Current?.MainWindow };
        if (dlg.ShowDialog() == true)
            await RefreshDestinationsAsync();
    }

    [RelayCommand]
    private async Task EditAsync(DestinationCard? card)
    {
        if (card is null || card.IsAddNew) return;

        var vm = new DestinationEditViewModel(_client)
        {
            Mode = DestinationEditMode.Edit,
            TripId = TripId,
            DestinationId = card.DestinationId,
            Title = card.Name,
            Description = card.Description,
            ImageUrl = card.ImageUrl
        };

        var dlg = new TripPlanner.Wpf.Views.DestinationEditDialog(vm) { Owner = Application.Current?.MainWindow };
        if (dlg.ShowDialog() == true)
            await RefreshDestinationsAsync();
    }

    // --- helpers ---
    private static string GetString(object obj, string name)
        => obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj)?.ToString() ?? "";

    private static string? GetNullableString(object obj, string name)
        => obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj)?.ToString();

    private static bool GetBool(object obj, string name)
        => (obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj) as bool?) ?? false;

    private static int GetInt(object obj, string name)
    {
        var v = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);
        if (v is int i) return i;
        if (v is long l) return (int)l;
        if (v is string s && int.TryParse(s, out var r)) return r;
        return 0;
    }

    private static string[]? GetStringArray(object obj, string name)
    {
        var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        var v = pi?.GetValue(obj);
        if (v is string[] arr) return arr;
        if (v is System.Collections.IEnumerable en)
            return en.Cast<object?>().Select(o => o?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        return null;
    }
}

public sealed partial class DestinationCard : ObservableObject
{
    public string DestinationId { get; set; } = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private int _voteCount;
    [ObservableProperty] private string[] _voterParticipantIds = Array.Empty<string>();
    [ObservableProperty] private bool _isMostVoted;
    [ObservableProperty] private bool _isSelectedVoterVoted;
    [ObservableProperty] private string? _imageUrl;
    public bool IsAddNew { get; private set; }
    public static DestinationCard CreateAddCard() => new DestinationCard { IsAddNew = true, Name = "Add…" };
}

public sealed record VoterOption(string? ParticipantId, string Label, bool IsMe)
{
    public override string ToString() => Label;
}
