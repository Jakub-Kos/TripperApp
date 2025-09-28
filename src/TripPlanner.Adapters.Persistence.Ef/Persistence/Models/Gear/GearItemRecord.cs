using System.ComponentModel.DataAnnotations.Schema;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Gear;

public sealed class GearItemRecord
{
    public Guid GearId { get; set; }
    public Guid TripId { get; set; }
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provisioning { get; set; } = "EACH"; // store as string enum
    public int? NeededQuantity { get; set; }
    public string TagsCsv { get; set; } = string.Empty; // simple storage for tags

    public TripRecord? Trip { get; set; }
    public List<GearAssignmentRecord> Assignments { get; set; } = new();

    [NotMapped]
    public IReadOnlyList<string> Tags
    {
        get => string.IsNullOrWhiteSpace(TagsCsv)
            ? Array.Empty<string>()
            : TagsCsv.Split('\u001F', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        set => TagsCsv = value is null ? string.Empty : string.Join('\u001F', value);
    }
}
