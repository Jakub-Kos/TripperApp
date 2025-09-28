using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public OverviewViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private bool _isFinished;

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        var s = await _client.GetTripByIdAsync(tripId);
        if (s != null)
        {
            Name = s.Name;
            IsFinished = s.IsFinished;
        }
        Description = await _client.GetTripDescriptionAsync(tripId) ?? string.Empty;
    }

    [RelayCommand]
    private async Task SaveDescriptionAsync()
    {
        await _client.SetTripDescriptionAsync(TripId, Description);
    }

    [RelayCommand]
    private async Task SetFinishedAsync(bool value)
    {
        await _client.UpdateTripStatusAsync(TripId, value);
        IsFinished = value;
    }
}