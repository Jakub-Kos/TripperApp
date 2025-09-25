using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;

namespace TripPlanner.Core.Validation.Validators;

public sealed class ProposeDestinationRequestValidator : AbstractValidator<ProposeDestinationRequest>
{
    public ProposeDestinationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.ImageUrls)
            .NotNull();

        RuleForEach(x => x.ImageUrls)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Each image URL must be an absolute URL.");
    }
}