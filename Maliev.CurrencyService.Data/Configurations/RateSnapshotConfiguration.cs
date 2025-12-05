using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Data.Configurations;

/// <summary>
/// Entity configuration for RateSnapshot
/// </summary>
public class RateSnapshotConfiguration : IEntityTypeConfiguration<RateSnapshot>
{
    /// <summary>
    /// Configures the entity of type <typeparamref name="TEntity" />.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<RateSnapshot> builder)
    {
        // Table and primary key
        builder.ToTable("rate_snapshots");
        builder.HasKey(rs => rs.Id);

        // Unique index - one rate per pair per day
        builder.HasIndex(rs => new { rs.FromCurrency, rs.ToCurrency, rs.SnapshotDate })
            .IsUnique()
            .HasDatabaseName("ix_rate_snapshots_from_to_date");

        // Index for batch queries
        builder.HasIndex(rs => rs.BatchId)
            .HasDatabaseName("ix_rate_snapshots_batch_id");

        // Index for date range queries and cleanup
        builder.HasIndex(rs => rs.SnapshotDate)
            .HasDatabaseName("ix_rate_snapshots_snapshot_date");

        // Column configurations
        builder.Property(rs => rs.BatchId)
            .IsRequired();

        builder.Property(rs => rs.FromCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(rs => rs.ToCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(rs => rs.Rate)
            .IsRequired()
            .HasPrecision(18, 6); // 6 decimal precision per SC-012

        builder.Property(rs => rs.SnapshotDate)
            .IsRequired()
            .HasColumnType("date"); // Explicit DATE type for PostgreSQL

        builder.Property(rs => rs.Source)
            .HasMaxLength(100);

        builder.Property(rs => rs.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
    }
}
