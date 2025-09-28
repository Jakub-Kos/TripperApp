namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Transportation;

public sealed class TransportationDocumentRecord
{
    public int Id { get; set; }
    public Guid TransportationId { get; set; }
    public string Url { get; set; } = default!; // relative URL under wwwroot
    public string ContentType { get; set; } = default!; // image/jpeg, image/png, application/pdf
    public string FileName { get; set; } = default!;
    public DateTimeOffset UploadedAt { get; set; }
}
