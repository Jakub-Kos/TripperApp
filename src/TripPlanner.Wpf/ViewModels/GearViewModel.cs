using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Gear;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class GearViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public GearViewModel(ITripPlannerClient client) => _client = client;

    // --- Trip context ---
    [ObservableProperty] private string _tripId = "";
    [ObservableProperty] private string _myParticipantId = "";
    [ObservableProperty] private int _participantCount;

    // --- Top bar: create item ---
    [ObservableProperty] private string _newGroup = "";
    [ObservableProperty] private string _newName = "";
    // 0 = per-person (EACH), 1 = shared (SHARED)
    [ObservableProperty] private int _newProvisioning;
    [ObservableProperty] private int? _newNeededQty;

    public ObservableCollection<GearItemRow> Items { get; } = new();

    // --- Load ---
    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        await RefreshParticipantsAsync();
        await RefreshGearAsync();
    }

    private async Task RefreshParticipantsAsync()
    {
        _myParticipantId = "";
        _participantCount = 0;

        var parts = await _client.ListParticipantsAsync(TripId);
        if (parts is not null)
        {
            foreach (var p in parts)
            {
                // Reflection on purpose (the WPF project avoids tight coupling to Contracts in some places)
                var t = p.GetType();
                var pid = t.GetProperty("ParticipantId")?.GetValue(p)?.ToString() ?? "";
                var isMe = (t.GetProperty("IsMe")?.GetValue(p) as bool?) ?? false;

                if (!string.IsNullOrWhiteSpace(pid))
                    _participantCount++;
                if (isMe) _myParticipantId = pid;
            }
        }

        OnPropertyChanged(nameof(ParticipantCount));
        OnPropertyChanged(nameof(MyParticipantId));
    }

    private async Task RefreshGearAsync()
    {
        Items.Clear();
        var list = await _client.ListGearAsync(TripId);
        if (list is null) return;

        foreach (var dto in list)
            Items.Add(GearItemRow.FromDto(dto, _myParticipantId, _participantCount));
    }

    // --- Create item ---
    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroup) || string.IsNullOrWhiteSpace(NewName))
            return;

        if (NewProvisioning == 1 && (NewNeededQty is null || NewNeededQty <= 0))
            return; // shared requires positive Needed

        var dto = await _client.CreateGearItemAsync(TripId,
            new CreateGearItemRequest(
                NewGroup.Trim(),
                NewName.Trim(),
                NewProvisioning == 0 ? GearProvisioning.EACH : GearProvisioning.SHARED,
                NewProvisioning == 1 ? NewNeededQty : null,
                Array.Empty<string>()));

        if (dto is not null)
            Items.Add(GearItemRow.FromDto(dto, _myParticipantId, _participantCount));

        NewGroup = "";
        NewName = "";
        NewNeededQty = null;
        NewProvisioning = 0;
    }

    // --- Delete item ---
    [RelayCommand]
    private async Task DeleteItemAsync(GearItemRow row)
    {
        var ok = await _client.DeleteGearItemAsync(TripId, row.GearId);
        if (ok) Items.Remove(row);
    }

    // --- Save my claim (upsert) ---
    [RelayCommand]
    private async Task SaveClaimAsync(GearItemRow row)
    {
        if (string.IsNullOrWhiteSpace(_myParticipantId)) return;

        var existing = row.Assignments.FirstOrDefault(a => a.ParticipantId == _myParticipantId);
        var qty = row.MyClaim;

        // delete if <= 0 or null and we had an assignment
        if ((qty is null || qty <= 0) && existing is not null)
        {
            var ok = await _client.DeleteGearAssignmentAsync(TripId, row.GearId, existing.AssignmentId);
            if (ok)
            {
                row.Assignments.Remove(existing);
                row.MyClaim = null;
                row.Recompute(_participantCount);
            }
            return;
        }

        if (qty is null || qty <= 0) return; // nothing to do

        // create if none exists
        if (existing is null)
        {
            var updated = await _client.CreateGearAssignmentAsync(
                TripId, row.GearId,
                new CreateGearAssignmentRequest(_myParticipantId, qty));

            if (updated is not null) ReplaceRow(updated);
            return;
        }

        // update existing
        var updated2 = await _client.UpdateGearAssignmentAsync(
            TripId, row.GearId, existing.AssignmentId,
            new CreateGearAssignmentRequest(_myParticipantId, qty));

        if (updated2 is not null) ReplaceRow(updated2);
    }

    // --- Delete my claim (shortcut) ---
    [RelayCommand]
    private async Task DeleteClaimAsync(GearItemRow row)
    {
        if (string.IsNullOrWhiteSpace(_myParticipantId)) return;
        var existing = row.Assignments.FirstOrDefault(a => a.ParticipantId == _myParticipantId);
        if (existing is null) return;

        var ok = await _client.DeleteGearAssignmentAsync(TripId, row.GearId, existing.AssignmentId);
        if (ok)
        {
            row.Assignments.Remove(existing);
            row.MyClaim = null;
            row.Recompute(_participantCount);
        }
    }

    // --- Optional preset example (kept from previous UI) ---
    [RelayCommand]
    private async Task BulkCreateCampingPresetAsync()
    {
        try
        {
            var ok = await _client.BulkCreateGearAsync(TripId, Presets.Camping());
            if (ok) await RefreshGearAsync();
        }
        catch (Exception)
        {
            // Swallow to avoid app crash if backend returns unexpected payload
            // TODO: optionally surface a user-friendly notification/logging
        }
    }

    // --- Utilities ---
    private void ReplaceRow(GearItemDto dto)
    {
        var idx = Items.IndexOf(Items.First(i => i.GearId == dto.GearId));
        Items[idx] = GearItemRow.FromDto(dto, _myParticipantId, _participantCount);
    }

}

// =============== Rows ===============

public sealed partial class GearItemRow : ObservableObject
{
    public string GearId { get; init; } = "";
    [ObservableProperty] private string _group = "";
    [ObservableProperty] private string _name = "";
    /// <summary>0 = per-person (EACH), 1 = shared (SHARED)</summary>
    [ObservableProperty] private int _provisioning;
    [ObservableProperty] private int? _neededQuantity;
    [ObservableProperty] private string[] _tags = Array.Empty<string>();

    public ObservableCollection<AssignmentRow> Assignments { get; } = new();

    // bound in UI (right side)
    [ObservableProperty] private int? _myClaim;

    // computed
    [ObservableProperty] private int _neededTotal;
    [ObservableProperty] private int _claimedTotal;
    [ObservableProperty] private int _leftTotal;

    public static GearItemRow FromDto(GearItemDto dto, string myParticipantId, int participantCount)
    {
        var row = new GearItemRow
        {
            GearId = dto.GearId,
            Group = dto.Group ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Provisioning = dto.Provisioning == GearProvisioning.EACH ? 0 : 1,
            NeededQuantity = dto.NeededQuantity,
            Tags = dto.Tags?.ToArray() ?? Array.Empty<string>(),
        };

        row.Assignments.Clear();
        if (dto.Assignments != null)
        {
            foreach (var a in dto.Assignments)
                row.Assignments.Add(new AssignmentRow(a.AssignmentId, a.ParticipantId, a.Quantity));
        }

        // initialize my claim from existing assignment
        var mine = row.Assignments.FirstOrDefault(a => a.ParticipantId == myParticipantId);
        row.MyClaim = mine?.Quantity;

        row.Recompute(participantCount);
        return row;
    }

    public void Recompute(int participantCount)
    {
        NeededTotal = Provisioning == 0
            ? Math.Max(0, participantCount)
            : Math.Max(0, NeededQuantity ?? 0);

        ClaimedTotal = Assignments.Sum(a => Math.Max(0, a.Quantity));
        LeftTotal = Math.Max(0, NeededTotal - ClaimedTotal);
    }
}

public sealed partial class AssignmentRow : ObservableObject
{
    public AssignmentRow(string id, string pid, int qty)
    {
        AssignmentId = id;
        ParticipantId = pid;
        _quantity = qty;
    }

    public string AssignmentId { get; }
    public string ParticipantId { get; }

    [ObservableProperty] private int _quantity;
}

// =============== Presets (optional) ===============
static class Presets
{
    public static BulkCreateGearRequest Camping()
        => new(new[]
        {
            new BulkGearGroup("Spani", new []
            {
                new BulkGearItem("Spacak",    GearProvisioning.EACH,  null, Array.Empty<string>()),
                new BulkGearItem("Karimatka", GearProvisioning.EACH,  null, Array.Empty<string>()),
                new BulkGearItem("Stan",      GearProvisioning.SHARED, 1,   new []{ "shared" })
            }),
            new BulkGearGroup("Vareni", new []
            {
                new BulkGearItem("Horak",     GearProvisioning.SHARED, 1,   new []{ "shared" }),
                new BulkGearItem("Zapalky",   GearProvisioning.EACH,  null, Array.Empty<string>())
            })
        });
}
