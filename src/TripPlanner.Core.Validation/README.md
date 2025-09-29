TripPlanner.Core.Validation

Purpose
- Provides request-level validation for API/client contracts using FluentValidation.
- Ensures inputs are well-formed before reaching the Application/Domain layers (e.g., IDs present, strings bounded, dates in an unambiguous format).

Scope and dependencies
- Depends on TripPlanner.Core.Contracts for DTOs to validate.
- Uses FluentValidation for rule definitions.
- Does not contain business logic; only guards shape/format and simple cross-field checks when necessary.

Where things live
- Validators/: All validators live here. One validator per request/DTO type.
- File naming: <RequestName>Validator.cs (e.g., CreateTripRequestValidator.cs).
- Class naming: public sealed class <RequestName>Validator : AbstractValidator<<RequestName>>

Conventions
- Keep rules concise and focused on data shape/format.
- Prefer NotEmpty/NotNull/MaximumLength for strings and collections.
- For identifiers in string form, validate GUID shape when appropriate.
- For dates, prefer ISO-8601 date strings (yyyy-MM-dd) to avoid locale ambiguity.
- Use Cascade(CascadeMode.Stop) where a short-circuit improves error messages.
- Keep helpers private static methods inside the validator if they are small and specific (e.g., IsGuid, BeIsoDate).
- Add brief XML summaries for each validator and its constructor; keep comments short and useful.

Philosophy
- Validation here is a protective boundary for the application. It should not duplicate domain invariants but should prevent obviously invalid requests.
- Favor predictable, user-friendly messages (e.g., "Name is required.", "Date must be in YYYY-MM-DD format.").

How to add a new validator
1) Add a new file in Validators/ named <RequestName>Validator.cs.
2) Derive from AbstractValidator<<RequestName>> and add rules for each field.
3) Reuse existing small helpers (e.g., IsGuid) or add a local private method if the check is specific.
4) Keep rule messages consistent and succinct.
5) Build and run tests.

Common snippets
- Required string with length:
  RuleFor(x => x.Name)
      .Cascade(CascadeMode.Stop)
      .NotEmpty().WithMessage("Name is required.")
      .MaximumLength(200).WithMessage("Name must be <= 200 chars.");

- GUID-shaped identifier:
  private static bool IsGuid(string s) => Guid.TryParse(s, out _);
  RuleFor(x => x.UserId)
      .NotEmpty().WithMessage("UserId is required.")
      .Must(IsGuid).WithMessage("UserId must be a GUID.");

- ISO date string (yyyy-MM-dd):
  private static bool BeIsoDate(string s) =>
      DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
  RuleFor(x => x.Date)
      .NotEmpty().WithMessage("Date is required.")
      .Must(BeIsoDate).WithMessage("Date must be in YYYY-MM-DD format.");

Registration/wiring
- Validators are typically discovered via DI container registration by scanning the assembly.
- In the API project, ensure that FluentValidation is added and assembly scanning includes TripPlanner.Core.Validation.
  Example (pseudo): services.AddValidatorsFromAssemblyContaining<CreateTripRequestValidator>();

Testing tips
- Write unit tests for each validator to cover: valid happy-path, missing required fields, boundary lengths, and format errors (GUID/date).
- Prefer asserting specific failure messages to keep API feedback consistent.

Folder layout
- Validators/: Request validators used by the API and other clients.

Versioning & compatibility
- This project is consumed by the API/Application layers at build time. Changes to validation rules can affect client behavior and tests.
- Prefer additive, non-breaking changes; coordinate stricter validations with client consumers and update tests accordingly.

Maintainers notes
- Keep comments concise and avoid duplicating domain/application rules here.
- If you need richer cross-field logic, consider whether it belongs in Application or Domain instead, and use validation only to guard the incoming shape.
