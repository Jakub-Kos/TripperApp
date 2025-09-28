using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Itinerary;

public sealed class DayRouteFileRecord
{
    [Key]
    public int RouteId { get; set; }
    public Guid DayId { get; set; }

    public string Url { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string MediaType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid UploadedByParticipantId { get; set; }

    // Navs
    public DayRecord Day { get; set; } = null!;
}
