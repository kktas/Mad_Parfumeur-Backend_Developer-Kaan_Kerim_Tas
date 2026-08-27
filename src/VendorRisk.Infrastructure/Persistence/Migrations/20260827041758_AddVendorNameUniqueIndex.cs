using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendorRisk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vendors_Name",
                table: "vendors");

            // Vendor names are unique irrespective of case, so the index is over lower("Name").
            // EF cannot express an index on an expression, hence the raw SQL. It also serves the
            // lower("Name") lookup the duplicate check performs, replacing the plain index above.
            migrationBuilder.Sql(
                """CREATE UNIQUE INDEX "IX_vendors_Name_lower" ON vendors (LOWER("Name"));""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_vendors_Name_lower";""");

            migrationBuilder.CreateIndex(
                name: "IX_vendors_Name",
                table: "vendors",
                column: "Name");
        }
    }
}
