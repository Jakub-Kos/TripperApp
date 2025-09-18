using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Validation.Validators;

public sealed class CastVoteRequestValidator : AbstractValidator<CastVoteRequest>
{
    public CastVoteRequestValidator()
    {
        RuleFor(x => x.DateOptionId)
            .NotEmpty().WithMessage("DateOptionId is required.")
            .Must(s => Guid.TryParse(s, out _)).WithMessage("DateOptionId must be a GUID.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.")
            .Must(s => Guid.TryParse(s, out _)).WithMessage("UserId must be a GUID.");
    }
}