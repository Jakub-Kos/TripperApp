using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public OverviewViewModel(ITripPlannerClient client) => _client = client;

    // Callback into MainViewModel status bar
    public Action<string>? ReportStatus { get; set; }

    // ------ Read-only state (bound in read-only view) ------
    [ObservableProperty] private string _tripId = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private bool _isFinished;

    // ------ Editing state (bound in edit view) ------
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editDescription = "";
    [ObservableProperty] private bool _editIsFinished;

    /// <summary>
    /// Loads trip overview and prepares read-only state.
    /// </summary>
    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        IsEditing = false;

        var s = await _client.GetTripByIdAsync(tripId);
        Name = s?.Name ?? "";
        // Prefer explicit finished flag names commonly used in your contracts
        IsFinished = (s?.IsFinished) ?? (s?.IsFinished ?? false);

        Description = await _client.GetTripDescriptionAsync(tripId) ?? string.Empty;

        // Keep edit fields in sync (so entering edit shows current values)
        EditName = Name;
        EditDescription = Description;
        EditIsFinished = IsFinished;
    }

    // ------ Commands ------

    [RelayCommand]
    private void EnterEdit()
    {
        EditName = Name;
        EditDescription = Description;
        EditIsFinished = IsFinished;
        IsEditing = true;
        ReportStatus?.Invoke("Editing trip overview…");
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        try
        {
            // Save only what really changed
            if (EditDescription != Description)
            {
                await _client.SetTripDescriptionAsync(TripId, EditDescription);
                Description = EditDescription;
                ReportStatus?.Invoke("Description saved.");
            }

            if (EditIsFinished != IsFinished)
            {
                await _client.UpdateTripStatusAsync(TripId, EditIsFinished);
                IsFinished = EditIsFinished;
                ReportStatus?.Invoke(EditIsFinished ? "Trip marked as finished." : "Trip re-opened.");
            }

            if (EditName != Name)
            {
                var ok = await _client.RenameTripAsync(TripId, EditName);
                if (ok)
                {
                    Name = EditName;
                    ReportStatus?.Invoke("Trip renamed.");
                }
                else
                {
                    ReportStatus?.Invoke("Trip not found. Rename failed.");
                }
            }
        }
        catch (Exception ex)
        {
            ReportStatus?.Invoke($"Failed to save changes: {ex.Message}");
            return;
        }
        finally
        {
            IsEditing = false;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        // Revert edit buffer back to read-only state
        EditName = Name;
        EditDescription = Description;
        EditIsFinished = IsFinished;
        IsEditing = false;
        ReportStatus?.Invoke("Edit cancelled.");
    }
}
