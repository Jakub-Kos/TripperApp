using System.Globalization;
using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Validation.Validators;

public sealed class CreateTripRequestValidator : AbstractValidator<CreateTripRequest>
{
    public CreateTripRequestValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be <= 200 chars.");

        RuleFor(x => x.OrganizerId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("OrganizerId is required.")
            .Must(BeGuid).WithMessage("OrganizerId must be a GUID.");
    }

    private static bool BeGuid(string s) => Guid.TryParse(s, out _);
}