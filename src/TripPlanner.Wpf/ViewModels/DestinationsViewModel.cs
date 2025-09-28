using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class DestinationsViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public DestinationsViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";

    public ObservableCollection<DestinationRow> Items { get; } = new();

    [ObservableProperty] private string _newTitle = "";
    [ObservableProperty] private string? _newDescription;
    [ObservableProperty] private string _proxyParticipantId = "";

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        await ReloadAll();
    }

    private async Task ReloadAll()
    {
        Items.Clear();

        var list = await _client.GetDestinationsAsync(TripId);
        if (list is null) return;
        foreach (var d in list)
        {
            var row = DestinationRow.FromDto(d);
            // Preload images from DTO if available
            row.Images.Clear();
            if (d.ImageUrls is not null)
            {
                foreach (var url in d.ImageUrls)
                {
                    var fileName = System.IO.Path.GetFileName(url ?? string.Empty);
                    row.Images.Add(new ImageRow(imageId: url ?? string.Empty, fileName: fileName, url: url));
                }
            }
            Items.Add(row);
        }
    }

    // Kept for compatibility; now images are part of the DTO, so this just ensures collection matches current row state.
    private Task ReloadImages(DestinationRow row)
    {
        // No separate API in ITripPlannerClient for destination images; they come with GetDestinationsAsync.
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ProposeAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle)) return;
        var req = new ProposeDestinationRequest(NewTitle, NewDescription, Array.Empty<string>());
        await _client.ProposeDestinationAsync(TripId, req);
        NewTitle = ""; NewDescription = null;
        await ReloadAll();
    }

    [RelayCommand]
    private async Task VoteSelfAsync(DestinationRow row)
    {
        // Backend uses current user identity; body is ignored by API but required by client signature
        await _client.VoteDestinationAsync(TripId, row.DestinationId, new VoteDestinationRequest(Guid.Empty));
        await RefreshRow(row);
    }

    [RelayCommand]
    private async Task VoteProxyAsync(DestinationRow row)
    {
        // ITripPlannerClient currently has no proxy vote method for destinations.
        // This command is a no-op if no participant id is provided.
        if (string.IsNullOrWhiteSpace(ProxyParticipantId)) return;
        // TODO: If proxy voting client method is introduced, wire it here.
        // For now, just refresh list so UI stays consistent.
        await RefreshRow(row);
    }

    [RelayCommand]
    private async Task ChooseAsync(DestinationRow row)
    {
        // Choosing a destination is not exposed in ITripPlannerClient; perform full reload.
        await ReloadAll();
    }

    [RelayCommand]
    private async Task DeleteAsync(DestinationRow row)
    {
        // Deleting a destination is not exposed in ITripPlannerClient; just remove locally for now.
        Items.Remove(row);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task UploadImageAsync(DestinationRow row)
    {
        // Image upload is not exposed in ITripPlannerClient; keep the dialog for UX but do nothing.
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp|All files|*.*",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true) return;
        // No API -> no upload; simply refresh.
        await ReloadImages(row);
    }

    private async Task RefreshRow(DestinationRow row)
    {
        // Re-fetch list and replace the row
        var list = await _client.GetDestinationsAsync(TripId);
        var fresh = list?.FirstOrDefault(d => d.DestinationId.ToString("D") == row.DestinationId);
        if (fresh is null) { await ReloadAll(); return; }

        var idx = Items.IndexOf(row);
        var updated = DestinationRow.FromDto(fresh);
        updated.Images.Clear();
        if (fresh.ImageUrls is not null)
        {
            foreach (var url in fresh.ImageUrls)
            {
                var fileName = System.IO.Path.GetFileName(url ?? string.Empty);
                updated.Images.Add(new ImageRow(imageId: url ?? string.Empty, fileName: fileName, url: url));
            }
        }
        Items[idx] = updated;
    }

    private static string GuessMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }
}

public sealed partial class DestinationRow : ObservableObject
{
    public string DestinationId { get; init; } = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string? _description;
    [ObservableProperty] private int _votes;
    [ObservableProperty] private bool _isChosen;

    public ObservableCollection<ImageRow> Images { get; } = new();

    public static DestinationRow FromDto(DestinationProposalDto d)
    {
        return new DestinationRow
        {
            DestinationId = d.DestinationId.ToString("D"),
            Title = d.Title,
            Description = d.Description,
            Votes = d.Votes,
            IsChosen = d.IsChosen
        };
    }
}

public sealed partial class ImageRow : ObservableObject
{
    public ImageRow(string imageId, string fileName, string? url)
    {
        ImageId = imageId; _fileName = fileName; _url = url;
    }

    public string ImageId { get; }
    [ObservableProperty] private string _fileName;
    [ObservableProperty] private string? _url;
}
