using System.ComponentModel.DataAnnotations;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Itinerary;

public sealed class DayItemRecord
{
    [Key]
    public Guid ItemId { get; set; }
    public Guid DayId { get; set; }

    public string Type { get; set; } = null!; // enum as string
    public string Name { get; set; } = null!;
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public string? ScheduledStart { get; set; } // HH:mm
    public int? DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public string? Link { get; set; }
    public int OrderIndex { get; set; }

    // Navs
    public DayRecord Day { get; set; } = null!;
}
