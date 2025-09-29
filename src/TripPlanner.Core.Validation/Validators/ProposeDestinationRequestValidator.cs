using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;

namespace TripPlanner.Core.Validation.Validators;

/// <summary>
/// Validator for ProposeDestinationRequest.
/// Validates title presence/length and that all image URLs are absolute and not overly long.
/// </summary>
public sealed class ProposeDestinationRequestValidator : AbstractValidator<ProposeDestinationRequest>
{
    /// <summary>
    /// Configures validation rules for proposing a new destination.
    /// </summary>
    public ProposeDestinationRequestValidator()
    {
        // Require a concise, non-empty title
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(256);

        // The collection must be provided; may be empty to indicate no images
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