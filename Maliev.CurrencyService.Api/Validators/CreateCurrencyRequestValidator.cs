using FluentValidation;
using Maliev.CurrencyService.Api.Models.Currencies;

namespace Maliev.CurrencyService.Api.Validators;

/// <summary>
/// Validator for currency creation requests
/// </summary>
public class CreateCurrencyRequestValidator : AbstractValidator<CreateCurrencyRequest>
{
    public CreateCurrencyRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Currency code is required")
            .Length(3)
            .WithMessage("Currency code must be exactly 3 characters (ISO 4217)")
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency code must be 3 uppercase letters");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Currency name is required")
            .MaximumLength(100)
            .WithMessage("Currency name must not exceed 100 characters");

        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Currency symbol is required")
            .MaximumLength(50)
            .WithMessage("Currency symbol must not exceed 50 characters");

        RuleFor(x => x.DecimalPlaces)
            .InclusiveBetween(0, 8)
            .WithMessage("Decimal places must be between 0 and 8");
    }
}
