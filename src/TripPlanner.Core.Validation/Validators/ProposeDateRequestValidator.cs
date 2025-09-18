using System.Globalization;
using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Validation.Validators;

public sealed class ProposeDateRequestValidator : AbstractValidator<ProposeDateRequest>
{
    public ProposeDateRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required.")
            .Must(BeIsoDate).WithMessage("Date must be in YYYY-MM-DD format.");
    }

    private static bool BeIsoDate(string s) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}