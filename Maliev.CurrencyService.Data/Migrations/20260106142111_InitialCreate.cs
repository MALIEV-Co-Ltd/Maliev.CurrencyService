using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.CurrencyService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    operation = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    changed_fields = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    decimal_places = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    version = table.Column<byte[]>(type: "bytea", nullable: false, defaultValue: new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currencies", x => x.id);
                    table.UniqueConstraint("ak_currencies_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    to_currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_transitive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    intermediate_currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: true),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exchange_rates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rate_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    to_currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staged_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    to_currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    validation_error = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staged_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "country_currencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_iso2 = table.Column<string>(type: "character varying(2)", unicode: false, maxLength: 2, nullable: false),
                    country_iso3 = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_country_currencies", x => x.id);
                    table.ForeignKey(
                        name: "fk_country_currencies_currencies_currency_code",
                        column: x => x.currency_code,
                        principalTable: "currencies",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_country_currencies_currency_code",
                table: "country_currencies",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "ix_country_iso2_currency",
                table: "country_currencies",
                columns: new[] { "country_iso2", "currency_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_country_iso3_currency",
                table: "country_currencies",
                columns: new[] { "country_iso3", "currency_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currencies_code",
                table: "currencies",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currencies_is_active",
                table: "currencies",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_expires_at",
                table: "exchange_rates",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_from_to",
                table: "exchange_rates",
                columns: new[] { "from_currency", "to_currency" });

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_from_to_fetched",
                table: "exchange_rates",
                columns: new[] { "from_currency", "to_currency", "fetched_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rate_snapshots_batch_id",
                table: "rate_snapshots",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_snapshots_from_to_date",
                table: "rate_snapshots",
                columns: new[] { "from_currency", "to_currency", "snapshot_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rate_snapshots_snapshot_date",
                table: "rate_snapshots",
                column: "snapshot_date");

            migrationBuilder.CreateIndex(
                name: "ix_staged_snapshots_batch_id",
                table: "staged_snapshots",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_staged_snapshots_status",
                table: "staged_snapshots",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "country_currencies");

            migrationBuilder.DropTable(
                name: "exchange_rates");

            migrationBuilder.DropTable(
                name: "rate_snapshots");

            migrationBuilder.DropTable(
                name: "staged_snapshots");

            migrationBuilder.DropTable(
                name: "currencies");
        }
    }
}
