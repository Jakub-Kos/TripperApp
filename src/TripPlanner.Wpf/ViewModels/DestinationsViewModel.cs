using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;

namespace TripPlanner.Wpf.ViewModels;

public sealed class DestinationsViewModel : INotifyPropertyChanged
{
    private readonly ITripPlannerClient _client;

    private string _tripId = "";
    private string? _tripName;
    private DestinationRow? _selectedDestination;
    private string? _newTitle;
    private string? _newDescription;
    private string? _newImagesRaw;
    private string? _voteUserId;
    private string? _status;
    private bool _busy;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<DestinationRow> Destinations { get; } = new();

    // When this changes we auto-refresh.
    public string TripId
    {
        get => _tripId;
        set
        {
            if (_tripId == value) return;
            _tripId = value ?? "";
            OnPropertyChanged();
            _ = RefreshCoreAsync(CancellationToken.None); // fire & forget on trip change
        }
    }

    public string? TripName
    {
        get => _tripName;
        set { _tripName = value; OnPropertyChanged(); }
    }

    public DestinationRow? SelectedDestination
    {
        get => _selectedDestination;
        set
        {
            if (_selectedDestination == value) return;
            _selectedDestination = value;
            OnPropertyChanged();
            ((AsyncCommand)VoteCommand).RaiseCanExecuteChanged();
        }
    }

    public string? NewTitle
    {
        get => _newTitle;
        set { _newTitle = value; OnPropertyChanged(); ((AsyncCommand)ProposeCommand).RaiseCanExecuteChanged(); }
    }

    public string? NewDescription
    {
        get => _newDescription;
        set { _newDescription = value; OnPropertyChanged(); }
    }

    public string? NewImagesRaw
    {
        get => _newImagesRaw;
        set { _newImagesRaw = value; OnPropertyChanged(); }
    }

    public string? VoteUserId
    {
        get => _voteUserId;
        set { _voteUserId = value; OnPropertyChanged(); ((AsyncCommand)VoteCommand).RaiseCanExecuteChanged(); }
    }

    public string? Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public bool Busy
    {
        get => _busy;
        private set
        {
            _busy = value; OnPropertyChanged();
            ((AsyncCommand)RefreshCommand).RaiseCanExecuteChanged();
            ((AsyncCommand)ProposeCommand).RaiseCanExecuteChanged();
            ((AsyncCommand)VoteCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ProposeCommand { get; }
    public ICommand VoteCommand { get; }

    public DestinationsViewModel(ITripPlannerClient client)
    {
        _client = client;
        RefreshCommand = new AsyncCommand(ct => GuardAsync(RefreshCoreAsync, ct), () => !Busy && !string.IsNullOrWhiteSpace(TripId));
        ProposeCommand = new AsyncCommand(ct => GuardAsync(ProposeCoreAsync, ct), () => !Busy && !string.IsNullOrWhiteSpace(TripId) && !string.IsNullOrWhiteSpace(NewTitle));
        VoteCommand    = new AsyncCommand(ct => GuardAsync(VoteCoreAsync,    ct), () => !Busy && !string.IsNullOrWhiteSpace(TripId) && SelectedDestination is not null && !string.IsNullOrWhiteSpace(VoteUserId));
    }

    private async Task GuardAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        try
        {
            Busy = true;
            await action(ct);
            Status ??= "OK";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task RefreshCoreAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(TripId))
        {
            Destinations.Clear();
            Status = "No trip selected.";
            return;
        }

        var list = await _client.GetDestinationsAsync(TripId, ct);
        if (list is null)
        {
            Destinations.Clear();
            Status = "Trip not found.";
            return;
        }

        Destinations.Clear();
        foreach (var d in list.OrderByDescending(x => x.Votes))
            Destinations.Add(new DestinationRow(d.DestinationId.ToString("D"), d.Title, d.Description, d.ImageUrls, d.Votes));

        Status = $"Loaded {Destinations.Count} destinations.";
    }

    private async Task ProposeCoreAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(TripId)) { Status = "No trip selected."; return; }
        if (string.IsNullOrWhiteSpace(NewTitle)) { Status = "Title is required."; return; }

        var urls = (NewImagesRaw ?? "")
            .Replace("\r", "")
            .Split(new[] { '\n', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        var req = new ProposeDestinationRequest(NewTitle!, string.IsNullOrWhiteSpace(NewDescription) ? null : NewDescription, urls);
        var createdId = await _client.ProposeDestinationAsync(TripId, req, ct);

        // reset & refresh
        NewTitle = null; NewDescription = null; NewImagesRaw = null;
        OnPropertyChanged(nameof(NewTitle));
        OnPropertyChanged(nameof(NewDescription));
        OnPropertyChanged(nameof(NewImagesRaw));

        await RefreshCoreAsync(ct);
        Status = string.IsNullOrWhiteSpace(createdId) ? "Proposed." : $"Proposed {createdId}.";
    }

    private async Task VoteCoreAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(TripId)) { Status = "No trip selected."; return; }
        if (SelectedDestination is null) { Status = "Select a destination."; return; }
        if (string.IsNullOrWhiteSpace(VoteUserId)) { Status = "Enter voter user id."; return; }

        var ok = await _client.VoteDestinationAsync(TripId, SelectedDestination.DestinationId, new VoteDestinationRequest(Guid.Parse(VoteUserId!)), ct);
        if (!ok) { Status = "Destination not found."; return; }

        await RefreshCoreAsync(ct);
        Status = "Vote cast.";
    }

    private void OnPropertyChanged([CallerMemberName] string? m = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));

    public sealed record DestinationRow(string DestinationId, string Title, string? Description, string[] ImageUrls, int Votes);
}

/// <summary>Minimal async ICommand</summary>
public sealed class AsyncCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _exec;
    private readonly Func<bool>? _can;
    private bool _busy;

    public AsyncCommand(Func<CancellationToken, Task> exec, Func<bool>? canExecute = null)
    {
        _exec = exec; _can = canExecute;
    }

    public bool CanExecute(object? parameter) => !_busy && (_can?.Invoke() ?? true);
    public event EventHandler? CanExecuteChanged;

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _busy = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _exec(CancellationToken.None); }
        finally { _busy = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
