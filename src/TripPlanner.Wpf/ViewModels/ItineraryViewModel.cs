using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Itinerary;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class ItineraryViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public ItineraryViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    public ObservableCollection<DayRow> Days { get; } = new();
    [ObservableProperty] private DayRow? _selectedDay;

    // For simpler command binding in XAML
    [ObservableProperty] private string _newDayDate = "";
    [ObservableProperty] private string? _newDayTitle;

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        Days.Clear();
        var list = await _client.ListDaysAsync(tripId);
        if (list is not null)
        {
            foreach (var d in list)
                Days.Add(DayRow.FromDto(d));
        }
        SelectedDay = Days.FirstOrDefault();
    }

    [RelayCommand] private async Task CreateDayAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDayDate)) return;
        var dto = await _client.CreateDayAsync(TripId, new CreateDayRequest(NewDayDate, NewDayTitle, null));
        if (dto is not null)
        {
            var row = DayRow.FromDto(dto);
            Days.Add(row);
            SelectedDay = row;
        }
        NewDayDate = ""; NewDayTitle = null;
    }

    [RelayCommand] private async Task UpdateDayAsync(DayRow row)
    {
        var ok = await _client.UpdateDayAsync(TripId, row.DayId, new UpdateDayRequest(row.Date, row.Title, row.Description));
        if (ok)
        {
            var dto = await _client.GetDayAsync(TripId, row.DayId);
            if (dto is not null) Replace(dto);
        }
    }

    [RelayCommand] private async Task DeleteDayAsync(DayRow row)
    {
        var ok = await _client.DeleteDayAsync(TripId, row.DayId);
        if (ok)
        {
            Days.Remove(row);
            if (ReferenceEquals(SelectedDay, row)) SelectedDay = Days.FirstOrDefault();
        }
    }

    [RelayCommand] private async Task UploadRouteAsync(DayRow row)
    {
        var dlg = new OpenFileDialog { Filter = "Routes|*.gpx;*.geojson;*.json|All|*.*" };
        if (dlg.ShowDialog() != true) return;
        using var fs = File.OpenRead(dlg.FileName);
        string fileName = System.IO.Path.GetFileName(dlg.FileName);
        string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        string contentType = ext switch
        {
            ".gpx" => "application/gpx+xml",
            ".geojson" => "application/geo+json",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
        var route = await _client.UploadDayRouteAsync(TripId, row.DayId, fs, fileName, contentType);
        if (route is not null)
        {
            await ReloadDay(row);
        }
    }

    [RelayCommand] private async Task DeleteRouteAsync(RouteRow route)
    {
        // Requires SelectedDay
        if (SelectedDay is null) return;
        var ok = await _client.DeleteDayRouteAsync(TripId, SelectedDay.DayId, route.RouteId);
        if (ok)
        {
            SelectedDay.Routes.Remove(route);
        }
    }

    private async Task ReloadDay(DayRow row)
    {
        var dto = await _client.GetDayAsync(TripId, row.DayId);
        if (dto is null) return;
        Replace(dto);
    }
    private void Replace(DayDto dto)
    {
        var idx = Days.ToList().FindIndex(d => d.DayId == dto.DayId);
        var row = DayRow.FromDto(dto);
        if (idx >= 0)
            Days[idx] = row;
        else
            Days.Add(row);
        if (SelectedDay?.DayId == dto.DayId) SelectedDay = row; // update selection to new instance
    }
}

public sealed partial class DayRow : ObservableObject
{
    public string DayId { get; set; } = "";
    [ObservableProperty] private string _date = "";
    [ObservableProperty] private string? _title;
    [ObservableProperty] private string? _description;
    public ObservableCollection<RouteRow> Routes { get; } = new();

    public static DayRow FromDto(DayDto d)
    {
        var r = new DayRow { DayId = d.DayId, Date = d.Date, Title = d.Title, Description = d.Description };
        foreach (var rf in d.Routes) r.Routes.Add(new RouteRow(rf.RouteId, rf.FileName, rf.MediaType, rf.SizeBytes));
        return r;
    }
}

public sealed record RouteRow(int RouteId, string FileName, string MediaType, long SizeBytes);
