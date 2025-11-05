using FluentValidation;
using Maliev.CurrencyService.Api.Models.Snapshots;

namespace Maliev.CurrencyService.Api.Validators;

/// <summary>
/// Validator for snapshot batch import requests
/// </summary>
public class SnapshotBatchRequestValidator : AbstractValidator<SnapshotBatchRequest>
{
    private const int MaxSnapshotsPerBatch = 5000;
    private const int MaxRetentionDays = 90; // FR-RET-001

    public SnapshotBatchRequestValidator()
    {
        RuleFor(x => x.SnapshotDate)
            .NotEmpty()
            .WithMessage("Snapshot date is required")
            .Must(BeWithinRetentionWindow)
            .WithMessage($"Snapshot date must be within the last {MaxRetentionDays} days");

        RuleFor(x => x.Source)
            .NotEmpty()
            .WithMessage("Source is required")
            .MaximumLength(100)
            .WithMessage("Source must not exceed 100 characters");

        RuleFor(x => x.Snapshots)
            .NotEmpty()
            .WithMessage("At least one snapshot entry is required")
            .Must(x => x != null && x.Count <= MaxSnapshotsPerBatch)
            .WithMessage($"Snapshot batch cannot exceed {MaxSnapshotsPerBatch} entries");

        RuleForEach(x => x.Snapshots)
            .ChildRules(snapshot =>
            {
                snapshot.RuleFor(s => s.From)
                    .NotEmpty()
                    .WithMessage("From currency is required")
                    .Length(3)
                    .WithMessage("Currency code must be 3 characters (ISO 4217)")
                    .Matches("^[A-Z]{3}$")
                    .WithMessage("Currency code must be uppercase letters only");

                snapshot.RuleFor(s => s.To)
                    .NotEmpty()
                    .WithMessage("To currency is required")
                    .Length(3)
                    .WithMessage("Currency code must be 3 characters (ISO 4217)")
                    .Matches("^[A-Z]{3}$")
                    .WithMessage("Currency code must be uppercase letters only");

                snapshot.RuleFor(s => s.Rate)
                    .GreaterThan(0)
                    .WithMessage("Exchange rate must be greater than zero")
                    .Must(HaveValidPrecision)
                    .WithMessage("Exchange rate must have at most 6 decimal places (FR-SC-012)");
            });
    }

    private static bool BeWithinRetentionWindow(DateOnly snapshotDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var oldestAllowed = today.AddDays(-MaxRetentionDays);

        return snapshotDate >= oldestAllowed && snapshotDate <= today;
    }

    private static bool HaveValidPrecision(decimal rate)
    {
        // Check if rate has at most 6 decimal places
        var rounded = Math.Round(rate, 6);
        return rate == rounded;
    }
}
