using FluentAssertions;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;
using Xunit;

public class TripTests
{
    [Fact]
    public void Create_sets_properties()
    {
        var organizer = UserId.New();
        var trip = Trip.Create("Weekend", organizer);

        trip.Name.Should().Be("Weekend");
        trip.OrganizerId.Should().Be(organizer);
    }
}