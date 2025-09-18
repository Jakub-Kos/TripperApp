namespace TripPlanner.Client.Configuration;

public sealed class TripPlannerClientOptions
{
    /// <summary>Base URL of the API, e.g. "http://localhost:5162".</summary>
    public required string BaseAddress { get; set; }
}