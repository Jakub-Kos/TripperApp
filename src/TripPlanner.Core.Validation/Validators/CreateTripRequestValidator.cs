using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Validation.Validators;

/// <summary>
/// Validator for CreateTripRequest.
/// Ensures the trip name is present and reasonably bounded.
/// </summary>
public sealed class CreateTripRequestValidator : AbstractValidator<CreateTripRequest>
{
    /// <summary>
    /// Configures validation rules for creating a new trip.
    /// </summary>
    public CreateTripRequestValidator()
    {
        // Name is a required human-facing label; keep it non-empty and within a sensible limit
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be <= 200 chars.");
    }
}