using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;

    public MainViewModel(
        ITripPlannerClient client,
        OverviewViewModel overview,
        ParticipantsViewModel participants,
        DatesViewModel dates,
        DestinationsViewModel destinations,
        GearViewModel gear,
        ItineraryViewModel itinerary,
        TransportationsViewModel transport,
        InvitesViewModel invites)
    {
        _client = client;
        Overview = overview;
        Participants = participants;
        Dates = dates;
        Destinations = destinations;
        Gear = gear;
        Itinerary = itinerary;
        Transport = transport;
        Invites = invites;
    }

    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _status = "";

    public ObservableCollection<TripListItem> Trips { get; } = new();

    [ObservableProperty] private TripListItem? _selectedTrip;

    public OverviewViewModel Overview { get; }
    public ParticipantsViewModel Participants { get; }
    public DatesViewModel Dates { get; }
    public DestinationsViewModel Destinations { get; }
    public GearViewModel Gear { get; }
    public ItineraryViewModel Itinerary { get; }
    public TransportationsViewModel Transport { get; }
    public InvitesViewModel Invites { get; }

    public async Task InitializeAsync() => await LoadTripsInternal();

    partial void OnSelectedTripChanged(TripListItem? value)
    {
        _ = InitializeTabsForSelection();
    }

    [RelayCommand]
    private async Task ReloadTripsAsync() => await LoadTripsInternal();

    [RelayCommand]
    private async Task CreateTripAsync()
    {
        try
        {
            Busy = true; Status = "Creating trip...";
            var created = await _client.CreateTripAsync(new CreateTripRequest("New Trip"));
            await LoadTripsInternal(created.TripId);
            Status = "Trip created.";
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }

    [RelayCommand]
    private async Task DeleteTripAsync()
    {
        if (SelectedTrip is null) return;
        try
        {
            Busy = true; Status = "Deleting trip...";
            // Delete trip is not available in current API; skipping.
            await Task.Delay(150);
            await LoadTripsInternal();
            Status = "Delete not supported in this build.";
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }

    private async Task LoadTripsInternal(string? selectTripId = null)
    {
        try
        {
            Busy = true; Status = "Loading trips...";
            Trips.Clear();
            var mine = await _client.ListMyTripsAsync(includeFinished: false);
            foreach (var t in mine)
                Trips.Add(new TripListItem(t.TripId, t.Name));
            SelectedTrip = Trips.FirstOrDefault(x => x.TripId == selectTripId) ?? Trips.FirstOrDefault();
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }

    private async Task InitializeTabsForSelection()
    {
        if (SelectedTrip is null) return;
        var tripId = SelectedTrip.TripId;

        try
        {
            Busy = true; Status = "Loading trip details...";
            // Overview loads summary + description
            await Overview.LoadAsync(tripId);

            // Other tabs
            await Participants.LoadAsync(tripId);
            await Dates.LoadAsync(tripId);
            await Destinations.LoadAsync(tripId);
            await Gear.LoadAsync(tripId);
            await Itinerary.LoadAsync(tripId);
            await Transport.LoadAsync(tripId);
            await Invites.LoadAsync(tripId);

            Status = "Ready.";
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }
}

public sealed record TripListItem(string TripId, string Name);
