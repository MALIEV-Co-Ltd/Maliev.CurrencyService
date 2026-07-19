using Maliev.CurrencyService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for StagedSnapshot.
/// </summary>
public class StagedSnapshotConfiguration : IEntityTypeConfiguration<StagedSnapshot>
{
    /// <summary>
    /// Configures the entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<StagedSnapshot> builder)
    {
        builder.ToTable("staged_snapshots");
        builder.HasKey(ss => ss.Id);

        builder.HasIndex(ss => ss.BatchId)
            .HasDatabaseName("ix_staged_snapshots_batch_id");

        builder.HasIndex(ss => ss.Status)
            .HasDatabaseName("ix_staged_snapshots_status");

        builder.Property(ss => ss.BatchId)
            .IsRequired();

        builder.Property(ss => ss.FromCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(ss => ss.ToCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(ss => ss.Rate)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.Property(ss => ss.SnapshotDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(ss => ss.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Pending");

        builder.Property(ss => ss.ValidationError)
            .HasColumnType("text");

        builder.Property(ss => ss.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
    }
}
