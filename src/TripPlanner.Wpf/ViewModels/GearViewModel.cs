using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Gear;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class GearViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public GearViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    public ObservableCollection<GearItemRow> Items { get; } = new();

    [ObservableProperty] private string _newGroup = "";
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private int _newProvisioning; // 0 = per-person, 1 = shared (matches enum) 
    [ObservableProperty] private int? _newNeededQty;

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        Items.Clear();
        var list = await _client.ListGearAsync(tripId);
        if (list is null) return;
        foreach (var g in list)
            Items.Add(GearItemRow.FromDto(g));
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dto = await _client.CreateGearItemAsync(TripId,
            new CreateGearItemRequest(_newGroup, _newName, (GearProvisioning)_newProvisioning, _newNeededQty, System.Array.Empty<string>()));
        if (dto is not null)
        {
            Items.Add(GearItemRow.FromDto(dto));
        }
        NewGroup = NewName = ""; NewNeededQty = null;
    }

    [RelayCommand] private async Task UpdateAsync(GearItemRow row)
    {
        var dto = await _client.UpdateGearItemAsync(TripId, row.GearId,
            new UpdateGearItemRequest(row.Group, row.Name, (GearProvisioning)row.Provisioning, row.NeededQuantity, row.Tags));
        if (dto is not null) Replace(dto);
    }

    [RelayCommand] private async Task DeleteAsync(GearItemRow row)
    {
        var ok = await _client.DeleteGearItemAsync(TripId, row.GearId);
        if (ok) Items.Remove(row);
    }

    private async Task AssignAsync(GearItemRow row, string participantId, int? qty)
    {
        var dto = await _client.CreateGearAssignmentAsync(TripId, row.GearId, new CreateGearAssignmentRequest(participantId, qty));
        if (dto is not null) Replace(dto);
    }

    private async Task UpdateAssignmentByIdsAsync(GearItemRow row, string assignmentId, int? qty)
    {
        var dto = await _client.UpdateGearAssignmentAsync(TripId, row.GearId, assignmentId,
            new CreateGearAssignmentRequest(row.AssignedFirstParticipantIdOrSelf()!, qty));
        if (dto is not null) Replace(dto);
    }

    private async Task UnassignAsync(GearItemRow row, string assignmentId)
    {
        var ok = await _client.DeleteGearAssignmentAsync(TripId, row.GearId, assignmentId);
        if (ok)
        {
            var a = row.Assignments.FirstOrDefault(x => x.AssignmentId == assignmentId);
            if (a is not null) row.Assignments.Remove(a);
        }
    }

    [RelayCommand]
    private async Task BulkCreateCampingPresetAsync()
    {
        var ok = await _client.BulkCreateGearAsync(TripId, Presets.Camping());
        if (ok) await LoadAsync(TripId);
    }

    [RelayCommand] private async Task AssignFromRowAsync(GearItemRow row)
    {
        if (string.IsNullOrWhiteSpace(row.NewAssignmentParticipantId)) return;
        var dto = await _client.CreateGearAssignmentAsync(TripId, row.GearId,
            new CreateGearAssignmentRequest(row.NewAssignmentParticipantId!, row.NewAssignmentQuantity));
        if (dto is not null) Replace(dto);
        row.NewAssignmentParticipantId = null; row.NewAssignmentQuantity = null;
    }

    [RelayCommand] private async Task UpdateAssignmentAsync(AssignmentRow a)
    {
        var parent = Items.FirstOrDefault(i => i.Assignments.Contains(a));
        if (parent is null) return;
        var dto = await _client.UpdateGearAssignmentAsync(TripId, parent.GearId, a.AssignmentId,
            new CreateGearAssignmentRequest(a.ParticipantId, a.Quantity));
        if (dto is not null) Replace(dto);
    }

    [RelayCommand] private async Task UnassignCommandAsync(AssignmentRow a)
    {
        var parent = Items.FirstOrDefault(i => i.Assignments.Contains(a));
        if (parent is null) return;
        var ok = await _client.DeleteGearAssignmentAsync(TripId, parent.GearId, a.AssignmentId);
        if (ok) parent.Assignments.Remove(a);
    }

    private void Replace(GearItemDto dto)
    {
        var idx = Items.ToList().FindIndex(x => x.GearId == dto.GearId);
        if (idx >= 0)
            Items[idx] = GearItemRow.FromDto(dto);
        else
            Items.Add(GearItemRow.FromDto(dto));
    }
}

public sealed partial class GearItemRow : ObservableObject
{
    public string GearId { get; set; } = "";
    [ObservableProperty] private string _group = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _provisioning;
    [ObservableProperty] private int? _neededQuantity;
    [ObservableProperty] private string[] _tags = System.Array.Empty<string>();
    [ObservableProperty] private string? _newAssignmentParticipantId;
    [ObservableProperty] private int? _newAssignmentQuantity;

    public ObservableCollection<AssignmentRow> Assignments { get; } = new();
    public string? AssignedFirstParticipantIdOrSelf() => Assignments.FirstOrDefault()?.ParticipantId;

    public static GearItemRow FromDto(GearItemDto dto)
    {
        var r = new GearItemRow
        {
            GearId = dto.GearId, Group = dto.Group, Name = dto.Name,
            Provisioning = (int)dto.Provisioning, NeededQuantity = dto.NeededQuantity, Tags = dto.Tags.ToArray()
        };
        foreach (var a in dto.Assignments) r.Assignments.Add(new AssignmentRow(a.AssignmentId, a.ParticipantId, a.Quantity));
        return r;
    }
}

public sealed partial class AssignmentRow : ObservableObject
{
    public AssignmentRow(string id, string pid, int qty) { AssignmentId = id; ParticipantId = pid; _quantity = qty; }
    public string AssignmentId { get; }
    public string ParticipantId { get; }
    [ObservableProperty] private int _quantity;
}

static class Presets
{
    public static BulkCreateGearRequest Camping()
        => new(new [] {
           new BulkGearGroup("Spani", new [] {
               new BulkGearItem("Spacak", GearProvisioning.EACH, null, System.Array.Empty<string>()),
               new BulkGearItem("Karimatka", GearProvisioning.EACH, null, System.Array.Empty<string>()),
               new BulkGearItem("Stan", GearProvisioning.SHARED, 1, new []{"shared"})
           }),
           new BulkGearGroup("Vareni", new [] {
               new BulkGearItem("Horak", GearProvisioning.SHARED, 1, new[]{"shared"}),
               new BulkGearItem("Zapalky", GearProvisioning.EACH, null, System.Array.Empty<string>())
           })
        });
}
