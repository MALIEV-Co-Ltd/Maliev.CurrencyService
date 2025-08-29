namespace Maliev.CurrencyService.Data.Data
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Maliev.CurrencyService.Data.Models;

    /// <summary>
    /// Represents the database context for currency-related data.
    /// </summary>
    public partial class CurrencyContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CurrencyContext"/> class.
        /// </summary>
        public CurrencyContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrencyContext"/> class with the specified options.
        /// </summary>
        /// <param name="options">The options for this context.</param>
        public CurrencyContext(DbContextOptions<CurrencyContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the DbSet for Currency entities.
        /// </summary>
        public required virtual DbSet<Currency> Currency { get; set; }

        /// <summary>
        /// Configures the database context.
        /// </summary>
        /// <param name="optionsBuilder">The builder used to construct the options for this context.</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // This method is intentionally left empty.
            // The connection string should be configured via dependency injection in Startup.cs.
        }

        /// <summary>
        /// Configures the model that was discovered by convention from the entity types exposed in DbSet properties on your derived context.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Currency>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.LongName)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.ModifiedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.ShortName)
                    .IsRequired()
                    .HasMaxLength(10);
            });
        }
    }
}