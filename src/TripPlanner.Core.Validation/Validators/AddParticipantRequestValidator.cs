using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Validation.Validators;

public sealed class AddParticipantRequestValidator : AbstractValidator<AddParticipantRequest>
{
    public AddParticipantRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}