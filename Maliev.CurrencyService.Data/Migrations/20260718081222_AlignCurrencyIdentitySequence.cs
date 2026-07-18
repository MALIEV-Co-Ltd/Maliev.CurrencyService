using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.CurrencyService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignCurrencyIdentitySequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SELECT setval(
                    pg_get_serial_sequence('"Currencies"', 'Id'),
                    GREATEST(
                        COALESCE(MAX("Id"), 1),
                        COALESCE(
                            pg_sequence_last_value(
                                pg_get_serial_sequence('"Currencies"', 'Id')::regclass),
                            1)),
                    MAX("Id") IS NOT NULL)
                FROM "Currencies";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sequence values are intentionally not rewound because doing so can
            // make future inserts collide with existing currency identifiers.
        }
    }
}
