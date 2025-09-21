using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Common;

public sealed class UserRecord
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    [Required, EmailAddress, MaxLength(254)]
    [Unicode(false)]
    public string Email { get; set; } = null!;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = null!;

    [Required, MaxLength(255)]
    [Unicode(false)]
    public string PasswordHash { get; set; } = null!;

    public ICollection<RefreshTokenRecord> RefreshTokens { get; set; } = new List<RefreshTokenRecord>();
}