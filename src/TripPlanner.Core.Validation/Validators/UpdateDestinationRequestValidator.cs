using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;

namespace TripPlanner.Core.Validation.Validators;

/// <summary>
/// Validator for UpdateDestinationRequest.
/// Mirrors the constraints of creation but applied to updates.
/// </summary>
public sealed class UpdateDestinationRequestValidator : AbstractValidator<UpdateDestinationRequest>
{
    /// <summary>
    /// Configures validation rules for updating an existing destination proposal.
    /// </summary>
    public UpdateDestinationRequestValidator()
    {
        // Title must remain concise and non-empty
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(256);

        // The collection must be present (may be empty)
        RuleFor(x => x.ImageUrls)
            .NotNull();

        // Each provided URL must be a non-empty absolute URI and reasonably bounded in length
        RuleForEach(x => x.ImageUrls)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Each image URL must be an absolute URL.");
    }
}
