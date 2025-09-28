using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;

public sealed class DateOptionRecord
{
    public Guid DateOptionId { get; set; }
    public Guid TripId { get; set; }
    public string DateIso { get; set; } = null!;
    public bool IsChosen { get; set; }
    public List<DateVoteRecord> Votes { get; set; } = new();
    public TripRecord Trip { get; set; } = default!;
}