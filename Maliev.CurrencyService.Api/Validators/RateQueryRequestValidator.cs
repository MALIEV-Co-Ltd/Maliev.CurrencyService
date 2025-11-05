using FluentValidation;
using Maliev.CurrencyService.Api.Models.Rates;

namespace Maliev.CurrencyService.Api.Validators;

/// <summary>
/// Validator for rate query requests
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Validates currency codes, mode, and date parameters.
/// </remarks>
public class RateQueryRequestValidator : AbstractValidator<RateQueryRequest>
{
    private static readonly string[] ValidModes = { "live", "snapshot" };

    public RateQueryRequestValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty().WithMessage("From currency is required")
            .Matches("^[A-Z]{3}$").WithMessage("From currency must be a 3-letter ISO 4217 code (e.g., USD)");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("To currency is required")
            .Matches("^[A-Z]{3}$").WithMessage("To currency must be a 3-letter ISO 4217 code (e.g., THB)");

        RuleFor(x => x.From)
            .NotEqual(x => x.To)
            .WithMessage("From and To currencies must be different");

        RuleFor(x => x.Mode)
            .Must(mode => ValidModes.Contains(mode?.ToLower()))
            .WithMessage("Mode must be 'live' or 'snapshot'");

        // Date is required when mode is snapshot
        When(x => string.Equals(x.Mode, "snapshot", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Date)
                .NotNull()
                .WithMessage("date is required when mode=snapshot");
        });

        // Date should not be provided for live mode
        When(x => string.Equals(x.Mode, "live", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Date)
                .Null()
                .WithMessage("Date should not be provided for live mode");
        });

        // Date cannot be in the future
        When(x => x.Date.HasValue, () =>
        {
            RuleFor(x => x.Date!.Value)
                .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Date cannot be in the future");
        });
    }
}
