using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Data.Configurations;

/// <summary>
/// Entity configuration for StagedSnapshot
/// </summary>
public class StagedSnapshotConfiguration : IEntityTypeConfiguration<StagedSnapshot>
{
    public void Configure(EntityTypeBuilder<StagedSnapshot> builder)
    {
        // Table and primary key
        builder.ToTable("staged_snapshots");
        builder.HasKey(ss => ss.Id);

        // Index for batch operations
        builder.HasIndex(ss => ss.BatchId)
            .HasDatabaseName("ix_staged_snapshots_batch_id");

        // Index for filtering by status (Pending, Validated, Rejected, Committed)
        builder.HasIndex(ss => ss.Status)
            .HasDatabaseName("ix_staged_snapshots_status");

        // Column configurations
        builder.Property(ss => ss.BatchId)
            .IsRequired();

        builder.Property(ss => ss.FromCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(ss => ss.ToCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(ss => ss.Rate)
            .IsRequired()
            .HasPrecision(18, 6); // 6 decimal precision per SC-012

        builder.Property(ss => ss.SnapshotDate)
            .IsRequired()
            .HasColumnType("date"); // Explicit DATE type for PostgreSQL

        builder.Property(ss => ss.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Pending");

        builder.Property(ss => ss.ValidationError)
            .HasColumnType("text"); // TEXT type for potentially long error messages

        builder.Property(ss => ss.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
    }
}
