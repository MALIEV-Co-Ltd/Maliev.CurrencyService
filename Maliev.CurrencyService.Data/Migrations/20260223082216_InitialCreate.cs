using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

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
                name: "batch_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    error_message = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_statuses", x => x.id);
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

            migrationBuilder.InsertData(
                table: "currencies",
                columns: new[] { "id", "code", "created_at", "decimal_places", "is_active", "name", "symbol", "updated_at", "version" },
                values: new object[,]
                {
                    { new Guid("00d04f8c-6061-558d-9920-e4c646fa8317"), "AZN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Azerbaijan Manat", "AZN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("035dce62-313b-5d94-a21c-f002f6b7c032"), "GBP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Pound Sterling", "GBP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("03b24222-bc1d-5201-b72f-68c542513829"), "CAD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Canadian Dollar", "CAD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("03e7fadd-67a8-5e87-94ac-18a727c50dbe"), "MXN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Mexican Peso", "MXN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("048b5050-8ea6-5b21-922a-dcb66d2fd5b5"), "ZAR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Rand", "ZAR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("0a5553e7-ff09-5c21-893d-c070eacc4fd7"), "BWP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Pula", "BWP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("0b664dfb-74b3-5090-b51d-70b6016f12bb"), "AFN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Afghani", "AFN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("0ba10dc3-4da7-5b95-b080-37ddcb1f865a"), "CHF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Swiss Franc", "CHF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("0c11fb91-b454-5a54-95ee-ef75c7540313"), "HTG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Gourde", "HTG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("0e61aac3-2e9d-587b-af44-f4728802e14f"), "ARS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Argentine Peso", "ARS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("0eabc243-65ab-5a7e-b7c0-4fcad575af65"), "USD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "US Dollar", "USD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("107bc6a3-e2c6-5166-8310-50dad1e60193"), "MYR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Malaysian Ringgit", "MYR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("128728f0-be2e-5d5d-8bee-6cdf16360cae"), "DJF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Djibouti Franc", "DJF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("1604e589-7542-5a5e-a31e-70e34b432d18"), "CHW", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "WIR Franc", "CHW", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("177ff8de-07bb-5133-a0c4-0a20fdc9ac36"), "COU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Unidad de Valor Real", "COU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("17fbe1df-ab2f-5ee4-a3e6-7aa767650e7d"), "OMR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Rial Omani", "OMR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("1abc81ba-455d-579e-8cd2-3741d29c52f4"), "CLP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Chilean Peso", "CLP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("1ad62e26-85a3-531a-9a99-4d4c7f9d1a4b"), "PKR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Pakistan Rupee", "PKR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("1b1c3430-bde3-5477-8d98-c5b6d2a8e34b"), "AOA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Kwanza", "AOA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("1ce15884-b7f2-5ad3-b23c-9976c280ea91"), "UZS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Uzbekistan Sum", "UZS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("1d97773d-4155-58ea-841f-c6fc274decf5"), "SCR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Seychelles Rupee", "SCR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("1eef44b7-1242-5c8f-b9a3-04dd1d2bef28"), "BGN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Bulgarian Lev", "BGN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("1f4f21c4-d1a0-5af3-bdcc-486f8c6d480e"), "ERN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Nakfa", "ERN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("212a45d1-278a-542b-91bb-fc9f215a4aa4"), "MZN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Mozambique Metical", "MZN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("22b06f67-1c29-57b4-88fa-aec6a4f4d9c2"), "KRW", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Won", "KRW", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("2562ab69-202f-5c6a-839e-90f69e9553c5"), "INR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Indian Rupee", "INR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("25a2b263-369b-54f9-bc1e-d0030629975b"), "IRR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Iranian Rial", "IRR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("26104f1f-4990-5b82-b258-2fc0276b81ea"), "SDG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Sudanese Pound", "SDG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("28191c96-8412-541e-b3f4-8d4521b560ca"), "BZD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Belize Dollar", "BZD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("2b0280fb-f003-54e8-a394-56b62af79e47"), "MRU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Ouguiya", "MRU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("2dd3cdae-eddb-5944-b851-adc2033a0060"), "PHP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Philippine Piso", "PHP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("2f6ed84b-3225-5adc-be10-3e7395313dfa"), "BAM", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Convertible Mark", "BAM", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("2fbc394c-0578-5cd8-a743-4337330ce9cb"), "NIO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Cordoba Oro", "NIO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("3534917e-1501-59b4-9c03-40c697200e89"), "BIF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Burundi Franc", "BIF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("36255726-3313-5cf2-9542-78ecff17bbf7"), "RWF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Rwanda Franc", "RWF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("36491270-747a-517b-8ff1-d95575265af7"), "WST", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Tala", "WST", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("379cacf6-eab4-5318-9d27-5f0489e2826f"), "LRD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Liberian Dollar", "LRD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("38c68e9c-df78-5b13-a4bf-6695f1165846"), "ZWL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Zimbabwe Dollar", "ZWL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("38fa6ea6-9b61-515c-aa88-97e0fa77d6b1"), "NZD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "New Zealand Dollar", "NZD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("3a40472d-13d4-5c3f-8d51-3bb719b3664f"), "HRK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Kuna", "HRK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("3b4c3126-023a-5760-a95c-f5cba56e36ea"), "PAB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Balboa", "PAB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("3ded8f55-4e09-5773-a75e-15620a81c80f"), "PGK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Kina", "PGK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("3fe0358e-ba73-52cc-bc0a-b207f16b944a"), "PEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Sol", "PEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("409f569e-1430-5ea2-9931-d6af8eca0042"), "KMF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Comorian Franc", "KMF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("42d99cb3-8272-5202-a69c-4526fd495d1a"), "SVC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "El Salvador Colon", "SVC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("43f1113a-546c-55a9-b986-9a53442a461b"), "GYD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Guyana Dollar", "GYD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("44b5c0fb-65fe-5416-85a4-c88c4c579b5a"), "MKD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Denar", "MKD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("451dfa94-3c55-5890-a4d4-2ecfa7104214"), "UYU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Peso Uruguayo", "UYU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("471174a6-e53c-5241-8a88-775944b8b665"), "TND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Tunisian Dinar", "TND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("4abc4f54-12c1-5645-b243-026e3a6b72f8"), "CVE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Cabo Verde Escudo", "CVE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("4c35e46e-d3b4-5522-bcb9-438c95d7313a"), "LSL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Loti", "LSL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("4c9c8901-2c6a-50bf-a88f-7c96df7854c9"), "KWD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Kuwaiti Dinar", "KWD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("4cbe6a17-3611-55ed-94c9-725485942beb"), "VEF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Bolívar", "VEF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("4d021bf7-e8b0-5508-b6e3-848e8e679cc8"), "VND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Dong", "VND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("4dc02709-e550-598a-bd71-3f1b8ce2e644"), "GNF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Guinean Franc", "GNF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("503b09c0-bb41-590c-9c3f-412f3f39dc14"), "UGX", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Uganda Shilling", "UGX", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5126254a-4e4b-52d6-900d-48c3092b6a14"), "SOS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Somali Shilling", "SOS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5205b6fd-8419-5716-8182-e10e024924be"), "TTD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Trinidad and Tobago Dollar", "TTD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("53d5a9df-a36e-5743-ad90-4b7ccdbf2829"), "XUA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "ADB Unit of Account", "XUA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("53f81853-8849-5024-9c6a-66738bf597fb"), "SLL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Leone", "SLL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("55654a14-505b-5931-a93f-0c68c596ba70"), "XOF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "CFA Franc BCEAO", "XOF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("583e755c-4245-5aa2-a3f6-5f0bb18db385"), "UYI", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Uruguay Peso en Unidades Indexadas (UI)", "UYI", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5958e4d2-4ac3-57e4-9b69-8552173a5840"), "CUP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Cuban Peso", "CUP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5a09356e-e654-5381-8f37-0ce8bfb3291c"), "KZT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Tenge", "KZT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5ad6bd2a-f3ef-5946-a976-acda759a2e85"), "LYD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Libyan Dinar", "LYD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5b37585b-47a1-56df-a2da-7998805b9add"), "GIP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Gibraltar Pound", "GIP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5beb6052-7098-5264-af9f-d325b1f79cb2"), "UAH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Hryvnia", "UAH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5cde17ba-8205-5f50-b8fc-4d79a389d8b8"), "TOP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Pa’anga", "TOP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5da2aca9-8b5b-5662-afdf-736301228961"), "MUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Mauritius Rupee", "MUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5eeeb906-c977-5532-af4a-38577d7585cc"), "DOP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Dominican Peso", "DOP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("5f35ce79-689c-58da-a410-084a7f6806d0"), "GMD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Dalasi", "GMD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6364813a-3936-5293-b61b-cd954956ee27"), "MOP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Pataca", "MOP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6475dee3-c4ef-55bc-9e14-515facb414ba"), "BMD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Bermudian Dollar", "BMD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6574e823-9f67-519a-983c-9cd89421646f"), "BOV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Mvdol", "BOV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6a8ad785-58d5-544c-98be-59decbe8af6b"), "IQD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Iraqi Dinar", "IQD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6bd7ae66-401a-529f-b0b9-eac83ba366d9"), "GHS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Ghana Cedi", "GHS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6d2cc0f3-9b87-598c-8e77-857d99e24da6"), "STN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Dobra", "STN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6dbf8bf1-bae4-5880-838c-0dfeac9700c2"), "XDR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "SDR (Special Drawing Right)", "XDR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6dc31208-9eff-58d6-91e6-bc50e3df3433"), "CNY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Yuan Renminbi", "CNY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6de2e4fa-b977-5003-8e24-c527dd87e03a"), "TZS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Tanzanian Shilling", "TZS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("6ecd80c6-5145-5d3b-9188-5be763b7698f"), "BBD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Barbados Dollar", "BBD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("72fbaec0-633d-5db9-aa80-682e07266145"), "LBP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Lebanese Pound", "LBP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("74c1473c-5945-5392-831a-449d75b521eb"), "BND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Brunei Dollar", "BND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("76a6728a-fe66-5264-a2a2-e1c9e61b3c7d"), "THB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Baht", "THB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("76e1e4f0-9d79-57ec-9202-28befdf2be56"), "TJS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Somoni", "TJS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("79e29c47-3abe-5e6e-b8fb-fd07b340daed"), "TMT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Turkmenistan New Manat", "TMT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("7b31c9ea-fcd1-50ba-8277-a6ff72640d3b"), "GTQ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Quetzal", "GTQ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("7cf23d99-7e0d-565d-8a3a-aee2fb424e47"), "ISK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Iceland Krona", "ISK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("80a0c4e7-614b-57a9-b830-9dd2c66196e7"), "EUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Euro", "EUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("80c02c41-b199-5c36-b925-2fc5c2197266"), "NAD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Namibia Dollar", "NAD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("82934dd7-414d-5dfd-a678-db928761393a"), "BDT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Taka", "BDT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("84772f85-aaa3-51ac-a5c3-5ed1b316f70d"), "EGP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Egyptian Pound", "EGP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("8540368c-dcc1-5d3c-8d78-477424f532bd"), "HKD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Hong Kong Dollar", "HKD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("86f5c8ff-98e4-5674-ac03-d2f8fb2e41f0"), "BTN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Ngultrum", "BTN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("8952a408-7d29-5698-8e2b-f2c4d52ac232"), "MXV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Mexican Unidad de Inversion (UDI)", "MXV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("8c3abbaa-e465-5b8b-9949-9a67fc3973c0"), "XCD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "East Caribbean Dollar", "XCD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("8cd0267e-bf51-5247-b8cf-e7ab5c06341d"), "SAR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Saudi Riyal", "SAR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("8d07b866-1912-5ec5-b9d2-ddf2de594e7d"), "LAK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Lao Kip", "LAK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("8ea7ace8-e579-5c6a-b744-ac84655dbb08"), "MMK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Kyat", "MMK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("9004241c-3454-5d60-b575-d7229cc54b02"), "NPR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Nepalese Rupee", "NPR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("9008ce10-78a3-5725-ab99-c6e22285ab05"), "CUC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Peso Convertible", "CUC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("900ef1f2-6c94-5b5f-a185-95cfcf295cbf"), "LKR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Sri Lanka Rupee", "LKR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("9122e9e7-c9db-53fd-ad8f-6ff7463c4cc7"), "KGS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Som", "KGS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("91d3948f-c340-5511-aa13-61d3e8829646"), "HNL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Lempira", "HNL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("96298897-8c52-52e1-891f-3a180c30524b"), "MDL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Moldovan Leu", "MDL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("97a009e8-3777-54a4-98f4-439e957cf1c0"), "JOD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Jordanian Dinar", "JOD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("97a2c01d-a01f-5cdf-8688-0435dd4f3f25"), "MGA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Malagasy Ariary", "MGA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("a3b7020e-3a9a-547b-92d7-33782014d047"), "YER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Yemeni Rial", "YER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("a79a46f8-e1de-50e2-a925-c66bde9b23f5"), "CHE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "WIR Euro", "CHE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("a7ca3c2b-c33a-512d-9076-88b4846d095a"), "ZMW", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Zambian Kwacha", "ZMW", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("aa592881-be64-5244-8f5b-88ddd7b779ab"), "IDR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Rupiah", "IDR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("ab851d51-efa3-599a-8074-898e8afad236"), "JPY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Yen", "JPY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("abe47642-7b01-5d46-b951-7a0e34d8fbf7"), "CRC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Costa Rican Colon", "CRC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("ac242c2b-fa0c-5abd-a115-4ec95eb7177a"), "CZK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Czech Koruna", "CZK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("ac69667b-936a-5468-9722-cb53f1b7e050"), "KHR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Riel", "KHR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("ad0809e7-b5da-521d-b026-8ebee914c200"), "TRY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Turkish Lira", "TRY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("ad324769-4d7d-514a-a710-c20ba386bf2e"), "ILS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "New Israeli Sheqel", "ILS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("adee4d79-07bb-5e7a-8068-acf35c8a5b05"), "BSD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Bahamian Dollar", "BSD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("b0296673-d608-513f-b0bf-b3cd3ce27a4f"), "BHD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Bahraini Dinar", "BHD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("b0591fe2-6ba4-57bd-a197-3a50c9ec917f"), "AUD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Australian Dollar", "AUD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("b1bf97bb-897c-5ea9-a1df-1fd70aa6597e"), "BYN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Belarusian Ruble", "BYN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("b30b8bf2-637f-564b-ab62-047b98b221b5"), "AMD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Armenian Dram", "AMD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("b549d9c7-497e-5134-bd83-ce9082b93254"), "ANG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Netherlands Antillean Guilder", "ANG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("b7e67d9c-2c6c-5e96-a5bb-0d89bb946e9c"), "SZL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Lilangeni", "SZL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("b9c3ef55-277a-5dc7-93dd-f9b70576a78e"), "MNT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Tugrik", "MNT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("bb0098b2-c8ab-5f0f-82ca-f7110fc7cab1"), "SBD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Solomon Islands Dollar", "SBD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("bb588bda-8638-5567-a860-f600bbbda9c9"), "XAF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "CFA Franc BEAC", "XAF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("bed15363-6783-584c-9a37-0f2ee876d7d2"), "NGN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Naira", "NGN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("bfc0b8ac-65ef-5322-87a3-a0bf9dbd547a"), "RUB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Russian Ruble", "RUB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c027ada2-236a-5b55-86ea-7546a9964e47"), "VUV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Vatu", "VUV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c23e533f-d9b0-514a-9548-276f789fd899"), "RSD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Serbian Dinar", "RSD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c250b1a7-10d7-5916-b58c-110466266c26"), "SHP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Saint Helena Pound", "SHP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c2c78f09-f999-5ab9-9c79-459e5f6211ea"), "ETB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Ethiopian Birr", "ETB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c38ec8d2-9fba-5df8-b4c1-17a12da1ef3e"), "BRL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Brazilian Real", "BRL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c3e38db3-c18b-54e0-9f62-6b7b07eb2117"), "SSP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "South Sudanese Pound", "SSP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c55815be-34d0-53de-8e03-bd63b06c0f0c"), "SYP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Syrian Pound", "SYP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c6bac4d7-f4f8-5877-b44e-1d5fa16cf71e"), "MAD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Moroccan Dirham", "MAD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c7132524-7eb9-5b9d-b0ab-6dd544e95690"), "PLN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Zloty", "PLN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("c8002ff9-758e-5ab1-afb0-46c8d92af05a"), "MVR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Rufiyaa", "MVR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("cc78e296-0db5-585e-a00c-b5da626395b5"), "SRD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Surinam Dollar", "SRD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("ccdf950c-3346-56ca-bfe9-3b3eeedd8109"), "KYD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Cayman Islands Dollar", "KYD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("cdb02a5a-6706-52d8-933c-88353a28f2e1"), "AED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "UAE Dirham", "AED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("cdc8b8c7-a11d-5b13-a9fa-b61053b896ad"), "AWG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Aruban Florin", "AWG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("d0b8f25d-05a7-51d1-aecc-1299a2cb7fff"), "FKP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Falkland Islands Pound", "FKP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("d25eb31c-df34-5445-9e79-d988232e4159"), "RON", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Romanian Leu", "RON", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("d2e5096c-f232-5be1-ae33-db2d94754426"), "ALL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Lek", "ALL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("d36a6593-2e3d-5a3a-a091-04a9eaa0a9f8"), "DKK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Danish Krone", "DKK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("d430e666-b6fc-5fd1-8959-ad9f46c57c93"), "BOB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Boliviano", "BOB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("d61b8fe4-4126-592c-b762-5ac7dad9447a"), "PYG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Guarani", "PYG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("d82910b0-610b-5947-af47-000e811df7bc"), "HUF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Forint", "HUF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("d8729e8b-c2a1-5889-b39d-1f0b4c85c4f8"), "MWK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Malawi Kwacha", "MWK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("da5194a4-6ed4-5b63-9f67-8bc4701f01ba"), "JMD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Jamaican Dollar", "JMD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("dd399551-870e-5a39-a1e0-c6367a3fa9ac"), "QAR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Qatari Rial", "QAR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("de60bca8-12f6-5209-b050-f3b8af0b0eb8"), "TWD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "New Taiwan Dollar", "TWD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("e11107f5-2d80-5ecc-9370-444e11fe1cdd"), "XSU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Sucre", "XSU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("ea7bd63c-f00b-5b10-81cc-496a34c101ed"), "CDF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Congolese Franc", "CDF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("eaf181af-85b8-5f39-8597-b211364b7a35"), "COP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Colombian Peso", "COP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("f287591a-ab71-5387-a4ac-229a7bd2d439"), "DZD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Algerian Dinar", "DZD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("f2a0f264-f8b2-5000-8c70-a27d12a5ba2a"), "SEK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Swedish Krona", "SEK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("f5129108-0701-5a71-947f-2c3ab4c37e1d"), "SGD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Singapore Dollar", "SGD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("f6ce496f-4a9d-5b58-a23c-5ac925193221"), "GEL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Lari", "GEL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("f7eb384c-9d48-58b7-8ba7-2503d039c6ed"), "USN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "US Dollar (Next day)", "USN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("f8e64dcb-3f3b-5f55-b1f6-d10e8f66804b"), "KES", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Kenyan Shilling", "KES", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("f92e6e38-1391-5d03-8863-d98e2569df46"), "KPW", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "North Korean Won", "KPW", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("fa931b22-ba5b-5f32-a27f-68387674f01c"), "CLF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Unidad de Fomento", "CLF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("faa03b00-8cd9-534a-8d4c-7e0178ed7f43"), "FJD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Fiji Dollar", "FJD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("fc4ea6ab-9eb5-53e3-931d-e4d9b5122682"), "XPF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "CFP Franc", "XPF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
                    { new Guid("ff49888c-1959-5231-ab4d-4c88611f166f"), "NOK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "Norwegian Krone", "NOK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } }
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
                name: "batch_statuses");

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
