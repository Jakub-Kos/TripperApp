using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Client.Errors;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject, INotifyPropertyChanged
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
    [ObservableProperty] private string _newParticipantUserId = "00000000-0000-0000-0000-000000000002";
    [ObservableProperty] private DateTime? _newDate = DateTime.Today.AddDays(7);
    [ObservableProperty] private string _voteUserId = "00000000-0000-0000-0000-000000000002";
    [ObservableProperty] private string? _selectedDateOptionId;

    public DestinationsViewModel DestinationsVm { get; }
    
    public MainViewModel(ITripPlannerClient client, DestinationsViewModel destinationsVm)
    {
        _client = client;
        DestinationsVm = destinationsVm;
    }
    
    private string? _selectedTripId;
    public string? SelectedTripId
    {
        get => _selectedTripId;
        set
        {
            if (_selectedTripId == value) return;
            _selectedTripId = value;
            OnPropertyChanged();
            DestinationsVm.TripId = _selectedTripId ?? string.Empty;     // or DestinationsVm.SetTrip(_selectedTripId)
            // optionally trigger an initial load:
            ((ICommand)DestinationsVm.RefreshCommand).Execute(null);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await GuardAsync(async () =>
        {
            Trips.Clear();
            foreach (var t in await _client.ListTripsAsync())
                Trips.Add(t);
            Status = $"Loaded {Trips.Count} trips.";
        });
    }

    partial void OnSelectedTripChanged(TripDto? value)
    {
        // Load summary in background (as you already do)
        _ = LoadSummaryAsync(value?.TripId);

        // Wire Destinations tab immediately using the selected trip id (string)
        DestinationsVm.TripId = value?.TripId ?? string.Empty;
        _ = DestinationsVm.RefreshCommand;
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
            Status = $"Created {created.TripId}";
            await RefreshAsync();
            SelectedTrip = Trips.FirstOrDefault(t => t.TripId == created.TripId);
        });
    }

    [RelayCommand]
    private async Task AddParticipantAsync()
    {
        if (SelectedTrip is null) { Status = "Select a trip."; return; }
        await GuardAsync(async () =>
        {
            var ok = await _client.AddParticipantAsync(SelectedTrip.TripId, new AddParticipantRequest(NewParticipantUserId));
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
            var id = await _client.ProposeDateOptionAsync(SelectedTrip.TripId,
                new ProposeDateRequest(DateOnly.FromDateTime(NewDate.Value).ToString("yyyy-MM-dd")));
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
            var ok = await _client.CastVoteAsync(SelectedTrip.TripId,
                new CastVoteRequest(SelectedDateOptionId!, VoteUserId));
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
