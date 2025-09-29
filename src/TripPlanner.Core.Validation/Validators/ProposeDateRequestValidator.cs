using System.Globalization;
using FluentValidation;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Validation.Validators;

/// <summary>
/// Validator for ProposeDateRequest.
/// Ensures the proposed date is provided in an ISO (YYYY-MM-DD) format for unambiguous parsing.
/// </summary>
public sealed class ProposeDateRequestValidator : AbstractValidator<ProposeDateRequest>
{
    /// <summary>
    /// Configures validation rules for proposing a date option.
    /// </summary>
    public ProposeDateRequestValidator()
    {
        // Require an ISO date string to avoid locale-specific ambiguities
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required.")
            .Must(BeIsoDate).WithMessage("Date must be in YYYY-MM-DD format.");
    }

    private static bool BeIsoDate(string s) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}