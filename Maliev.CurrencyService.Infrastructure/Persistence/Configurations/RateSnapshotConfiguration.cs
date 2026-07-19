using Maliev.CurrencyService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for RateSnapshot.
/// </summary>
public class RateSnapshotConfiguration : IEntityTypeConfiguration<RateSnapshot>
{
    /// <summary>
    /// Configures the entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<RateSnapshot> builder)
    {
        builder.ToTable("rate_snapshots");
        builder.HasKey(rs => rs.Id);

        builder.HasIndex(rs => new { rs.FromCurrency, rs.ToCurrency, rs.SnapshotDate })
            .IsUnique()
            .HasDatabaseName("ix_rate_snapshots_from_to_date");

        builder.HasIndex(rs => rs.BatchId)
            .HasDatabaseName("ix_rate_snapshots_batch_id");

        builder.HasIndex(rs => rs.SnapshotDate)
            .HasDatabaseName("ix_rate_snapshots_snapshot_date");

        builder.Property(rs => rs.BatchId)
            .IsRequired();

        builder.Property(rs => rs.FromCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(rs => rs.ToCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(rs => rs.Rate)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.Property(rs => rs.SnapshotDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(rs => rs.Source)
            .HasMaxLength(100);

        builder.Property(rs => rs.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
    }
}
