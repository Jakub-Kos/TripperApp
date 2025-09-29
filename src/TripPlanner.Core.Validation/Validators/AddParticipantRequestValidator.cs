using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Validation.Validators;

/// <summary>
/// Validator for AddParticipantRequest.
/// Ensures a valid participant identifier is provided.
/// </summary>
public sealed class AddParticipantRequestValidator : AbstractValidator<AddParticipantRequest>
{
    /// <summary>
    /// Configures validation rules for adding a participant to a trip.
    /// </summary>
    public AddParticipantRequestValidator()
    {
        // Require a non-empty user identifier (format is validated downstream when mapping to a typed ID).
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}