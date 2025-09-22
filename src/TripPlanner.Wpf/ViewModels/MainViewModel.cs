using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Client.Errors;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;

    [ObservableProperty] private string? _status;
    [ObservableProperty] private bool _busy;

    // Create trip
    [ObservableProperty] private string _newTripName = "";
    [ObservableProperty] private string _organizerId = "00000000-0000-0000-0000-000000000001";

    // Selection & summary
    public ObservableCollection<TripDto> Trips { get; } = new();

    [ObservableProperty] private TripDto? _selectedTrip;
    [ObservableProperty] private TripSummaryDto? _selectedSummary;

    // Participant & date actions
    [ObservableProperty] private string? _newParticipantUserIdText;
    [ObservableProperty] private string _newParticipantDisplayName = "";
    [ObservableProperty] private DateTime? _newDate = DateTime.Today.AddDays(7);
    [ObservableProperty] private string _voteUserId = "00000000-0000-0000-0000-000000000002";
    [ObservableProperty] private string? _selectedDateOptionId;

    // Optional: also expose an ID-only selection for XAML that binds SelectedValuePath="TripId"
    private string? _selectedTripId;
    public string? SelectedTripId
    {
        get => _selectedTripId;
        set
        {
            if (_selectedTripId == value) return;
            _selectedTripId = value;
            OnPropertyChanged();

            // Keep SelectedTrip in sync when binding uses SelectedValue (TripId)
            var match = Trips.FirstOrDefault(t => t.TripId == value);
            if (!ReferenceEquals(SelectedTrip, match))
                SelectedTrip = match;
        }
    }

    public DestinationsViewModel DestinationsVm { get; }

    public MainViewModel(ITripPlannerClient client, DestinationsViewModel destinationsVm)
    {
        _client = client;
        DestinationsVm = destinationsVm;
        // IMPORTANT: No fire-and-forget here. Call InitializeAsync() from App after sign-in.
    }

    /// <summary>
    /// Call this from App.xaml.cs after successful sign-in, and AWAIT it.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await GuardAsync(async () =>
        {
            await LoadTripsAsync(ct);

            // Auto-select first trip on initial load -> this will also drive Destinations tab
            if (Trips.Count > 0 && SelectedTrip is null && SelectedTripId is null)
                SelectedTrip = Trips[0];
        });
    }

    private async Task LoadTripsAsync(CancellationToken ct)
    {
        var page = await _client.ListTripsAsync(skip: 0, take: 50, ct);
        Trips.Clear();
        foreach (var t in page)
            Trips.Add(new TripDto(t.TripId, t.Name, t.OrganizerId));

        Status = $"Loaded {Trips.Count} trips.";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await GuardAsync(async () =>
        {
            await LoadTripsAsync(CancellationToken.None);

            // keep current selection by id if possible
            if (SelectedTrip is not null)
            {
                var keep = Trips.FirstOrDefault(t => t.TripId == SelectedTrip.TripId);
                SelectedTrip = keep ?? Trips.FirstOrDefault();
            }
            else if (SelectedTripId is not null)
            {
                var keep = Trips.FirstOrDefault(t => t.TripId == SelectedTripId);
                SelectedTrip = keep ?? Trips.FirstOrDefault();
            }
        });
    }

    partial void OnSelectedTripChanged(TripDto? value)
    {
        // Keep SelectedTripId in sync (when binding uses SelectedItem)
        var newId = value?.TripId;
        if (_selectedTripId != newId)
        {
            _selectedTripId = newId;
            OnPropertyChanged(nameof(SelectedTripId));
        }

        // Load summary (async) and wire Destinations tab
        _ = LoadSummaryAsync(newId);

        DestinationsVm.TripId   = newId ?? string.Empty;
        DestinationsVm.TripName = value?.Name;

        // Kick a refresh on the Destinations tab (if user already opened it)
        if (DestinationsVm.RefreshCommand.CanExecute(null))
            DestinationsVm.RefreshCommand.Execute(null);
    }

    private async Task LoadSummaryAsync(string? tripId)
    {
        if (string.IsNullOrWhiteSpace(tripId)) { SelectedSummary = null; return; }

        await GuardAsync(async () =>
        {
            SelectedSummary = await _client.GetTripByIdAsync(tripId);
            SelectedDateOptionId = SelectedSummary?.DateOptions.FirstOrDefault()?.DateOptionId;
        });
    }

    [RelayCommand]
    private async Task CreateTripAsync()
    {
        await GuardAsync(async () =>
        {
            var created = await _client.CreateTripAsync(new CreateTripRequest(NewTripName, OrganizerId));
            await RefreshAsync();
            SelectedTrip = Trips.FirstOrDefault(t => t.TripId == created.TripId);
            Status = $"Created trip “{created.Name}”.";
        });
    }

    [RelayCommand]
    private async Task AddParticipantAsync()
    {
        if (SelectedTrip is null) { Status = "Select a trip."; return; }

        await GuardAsync(async () =>
        {
            var req = Guid.TryParse(NewParticipantUserIdText, out var userId)
                ? new AddParticipantRequest(userId, "")
                : new AddParticipantRequest(null, NewParticipantDisplayName);

            if (req.UserId is null && string.IsNullOrWhiteSpace(req.DisplayName))
            {
                Status = "Enter a valid UserId or a display name.";
                return;
            }

            var ok = await _client.AddParticipantAsync(SelectedTrip.TripId, req);
            Status = ok ? "Participant added." : "Trip not found.";
            await LoadSummaryAsync(SelectedTrip.TripId);
        });
    }

    [RelayCommand]
    private async Task ProposeDateAsync()
    {
        if (SelectedTrip is null) { Status = "Select a trip."; return; }
        if (NewDate is null) { Status = "Pick a date."; return; }

        await GuardAsync(async () =>
        {
            var id = await _client.ProposeDateOptionAsync(
                SelectedTrip.TripId,
                new ProposeDateRequest(DateOnly.FromDateTime(NewDate.Value).ToString("yyyy-MM-dd"))
            );

            Status = id is not null ? $"Date option {id} proposed." : "Trip not found.";
            await LoadSummaryAsync(SelectedTrip.TripId);
        });
    }

    [RelayCommand]
    private async Task CastVoteAsync()
    {
        if (SelectedTrip is null) { Status = "Select a trip."; return; }
        if (string.IsNullOrWhiteSpace(SelectedDateOptionId)) { Status = "Pick a date option."; return; }

        await GuardAsync(async () =>
        {
            var ok = await _client.CastVoteAsync(
                SelectedTrip.TripId,
                new CastVoteRequest(SelectedDateOptionId!, VoteUserId)
            );

            Status = ok ? "Vote cast." : "Option not found.";
            await LoadSummaryAsync(SelectedTrip.TripId);
        });
    }

    // Small helper for UI-safe error handling
    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            Busy = true;
            await action();
        }
        catch (ApiException ex)
        {
            // Show friendly API errors (401/403/validation/etc.)
            Status = ex.Error?.Message ?? ex.Message;
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
}
