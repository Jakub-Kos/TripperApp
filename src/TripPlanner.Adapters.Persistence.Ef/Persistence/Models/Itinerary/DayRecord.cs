using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Itinerary;

public sealed class DayRecord
{
    [Key]
    public Guid DayId { get; set; }
    public Guid TripId { get; set; }
    public string Date { get; set; } = null!; // ISO yyyy-MM-dd
    public string? Title { get; set; }
    public string? Description { get; set; }

    // Anchors (owned)
    public LocationEmbeddable? StartLocation { get; set; }
    public LocationEmbeddable? EndLocation { get; set; }

    // Navs
    public TripRecord Trip { get; set; } = null!;
    public ICollection<DayItemRecord> Items { get; set; } = new List<DayItemRecord>();
    public ICollection<DayRouteFileRecord> Routes { get; set; } = new List<DayRouteFileRecord>();
}

public sealed class LocationEmbeddable
{
    public string? Name { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string? Address { get; set; }
    public string? PlaceId { get; set; }
}
