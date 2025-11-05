using FluentValidation;
using Maliev.CurrencyService.Api.Models.Currencies;

namespace Maliev.CurrencyService.Api.Validators;

/// <summary>
/// Validator for currency update requests
/// </summary>
public class UpdateCurrencyRequestValidator : AbstractValidator<UpdateCurrencyRequest>
{
    public UpdateCurrencyRequestValidator()
    {
        // Version is optional - optimistic concurrency is handled via ETag/If-Match header

        RuleFor(x => x.Name)
            .MaximumLength(100)
            .WithMessage("Currency name must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Symbol)
            .MaximumLength(50)
            .WithMessage("Currency symbol must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Symbol));

        RuleFor(x => x.DecimalPlaces)
            .InclusiveBetween(0, 8)
            .WithMessage("Decimal places must be between 0 and 8")
            .When(x => x.DecimalPlaces.HasValue);

        // At least one field must be provided for update
        RuleFor(x => x)
            .Must(x => x.Name != null || x.Symbol != null || x.DecimalPlaces.HasValue || x.IsActive.HasValue)
            .WithMessage("At least one field must be provided for update");
    }
}
