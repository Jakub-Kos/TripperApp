using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Validation.Validators;

/// <summary>
/// Validator for CastVoteRequest.
/// Ensures both the date option ID and user ID are present and in GUID format.
/// </summary>
public sealed class CastVoteRequestValidator : AbstractValidator<CastVoteRequest>
{
    /// <summary>
    /// Configures validation rules for casting a vote for a specific date option.
    /// </summary>
    public CastVoteRequestValidator()
    {
        // Validate DateOptionId presence and format
        RuleFor(x => x.DateOptionId)
            .NotEmpty().WithMessage("DateOptionId is required.")
            .Must(IsGuid).WithMessage("DateOptionId must be a GUID.");

        // Validate UserId presence and format
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.")
            .Must(IsGuid).WithMessage("UserId must be a GUID.");
    }

    private static bool IsGuid(string s) => Guid.TryParse(s, out _);
}