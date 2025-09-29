using FluentValidation;
using Microsoft.AspNetCore.Http;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Api.Infrastructure.Validation;

/// <summary>
/// Minimal API endpoint filter that runs a FluentValidation validator for the incoming request DTO.
/// If validation fails, returns a 400 response with aggregated error messages.
/// </summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var validator = ctx.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null) return await next(ctx);

        var model = ctx.Arguments.OfType<TRequest>().FirstOrDefault();
        if (model is null) return await next(ctx);

        var result = await validator.ValidateAsync(model, ctx.HttpContext.RequestAborted);
        if (result.IsValid) return await next(ctx);

        var details = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return Results.BadRequest(new ErrorResponse(ErrorCodes.Validation, "Validation failed.", details));
    }
}