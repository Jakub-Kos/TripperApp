using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;

namespace TripPlanner.Wpf.ViewModels;

public enum DestinationEditMode { Create, Edit }

/// <summary>
/// Dialog ViewModel for creating or editing a destination proposal.
/// </summary>
public sealed partial class DestinationEditViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public DestinationEditViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private DestinationEditMode _mode = DestinationEditMode.Create;
    [ObservableProperty] private string _tripId = "";
    [ObservableProperty] private string _destinationId = "";

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string? _imageUrl;

    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _status = "";

    public bool IsEdit => Mode == DestinationEditMode.Edit;
    public Action<bool>? Close;

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            Busy = true; Status = "Saving…";

            if (Mode == DestinationEditMode.Create)
            {
                // POST /api/v1/trips/{tripId}/destinations   { title, description, imageUrls[] }
                await _client.ProposeDestinationAsync(TripId, new ProposeDestinationRequest(Title, string.IsNullOrWhiteSpace(Description) ? null : Description,
                    string.IsNullOrWhiteSpace(ImageUrl) ? Array.Empty<string>() : new[] { ImageUrl! }));
            }
            else
            {
                // PATCH /api/v1/trips/{tripId}/destinations/{destinationId}
                var ok = await _client.UpdateDestinationAsync(TripId, DestinationId, new UpdateDestinationRequest(Title, string.IsNullOrWhiteSpace(Description) ? null : Description,
                    string.IsNullOrWhiteSpace(ImageUrl) ? Array.Empty<string>() : new[] { ImageUrl! }));
                if (!ok) throw new InvalidOperationException("Update failed (not found or forbidden).");
            }

            Close?.Invoke(true);
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!IsEdit) { Close?.Invoke(false); return; }
        try
        {
            Busy = true; Status = "Deleting…";
            await _client.DeleteDestinationAsync(TripId, DestinationId);
            Close?.Invoke(true);
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { Busy = false; }
    }

    [RelayCommand]
    private void Cancel() => Close?.Invoke(false);
}
