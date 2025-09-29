using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

/// <summary>
/// Transportations tab: manage transport options and attach route files or documents; organizer can choose one.
/// </summary>
public sealed partial class TransportationsViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public TransportationsViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    public ObservableCollection<TransportationRow> Items { get; } = new();

    [ObservableProperty] private string _newTitle = "";
    [ObservableProperty] private string? _newDescription;

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        Items.Clear();
        var list = await _client.ListTransportationsAsync(tripId);
        if (list is null) return;
        foreach (var t in list) Items.Add(new(t.TransportationId, t.Title, t.Description, t.IsChosen));
    }

    [RelayCommand] private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle)) return;
        var id = await _client.CreateTransportationAsync(TripId, NewTitle, NewDescription);
        if (id is not null)
        {
            await LoadAsync(TripId);
        }
        NewTitle = ""; NewDescription = null;
    }

    [RelayCommand] private async Task ChooseAsync(TransportationRow row) { var ok = await _client.ChooseTransportationAsync(TripId, row.TransportationId); if (ok) await LoadAsync(TripId); }
    [RelayCommand] private async Task DeleteAsync(TransportationRow row) { var ok = await _client.DeleteTransportationAsync(TripId, row.TransportationId); if (ok) Items.Remove(row); }

    [RelayCommand] private async Task UploadRouteAsync(TransportationRow row)
    {
        var dlg = new OpenFileDialog { Filter = "Routes|*.gpx;*.geojson;*.json|All|*.*" };
        if (dlg.ShowDialog() != true) return;
        using var fs = File.OpenRead(dlg.FileName);
        string fileName = Path.GetFileName(dlg.FileName);
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        string contentType = ext switch
        {
            ".gpx" => "application/gpx+xml",
            ".geojson" => "application/json",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
        await _client.UploadTransportationRouteAsync(TripId, row.TransportationId, fs, fileName, contentType);
        await LoadAsync(TripId);
    }

    [RelayCommand] private async Task UploadDocumentAsync(TransportationRow row)
    {
        var dlg = new OpenFileDialog { Filter = "Documents|*.png;*.jpg;*.jpeg;*.pdf|All|*.*" };
        if (dlg.ShowDialog() != true) return;
        using var fs = File.OpenRead(dlg.FileName);
        string fileName = Path.GetFileName(dlg.FileName);
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        string contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
        await _client.UploadTransportationDocumentAsync(TripId, row.TransportationId, fs, fileName, contentType);
        await LoadAsync(TripId);
    }
}

public sealed partial class TransportationRow : ObservableObject
{
    public TransportationRow(string id, string title, string? desc, bool chosen)
    { TransportationId = id; _title = title; _description = desc; _isChosen = chosen; }
    public string TransportationId { get; }
    [ObservableProperty] private string _title;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private bool _isChosen;
}
