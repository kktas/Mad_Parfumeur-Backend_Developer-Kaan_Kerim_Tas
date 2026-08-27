using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VendorRisk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "certificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vendor_certificates",
                columns: table => new
                {
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    CertificateId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_certificates", x => new { x.VendorId, x.CertificateId });
                    table.ForeignKey(
                        name: "FK_vendor_certificates_certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vendor_certificates_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_certificates_Code",
                table: "certificates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vendor_certificates_CertificateId",
                table: "vendor_certificates",
                column: "CertificateId");

            // Carry the certificates already recorded in the text[] column into the catalogue, one
            // row per distinct code, before that column goes away. A no-op on a fresh database.
            migrationBuilder.Sql("""
                INSERT INTO certificates ("Code", "Name")
                SELECT DISTINCT upper(btrim(cert)), upper(btrim(cert))
                FROM vendors vendor
                CROSS JOIN LATERAL unnest(vendor."SecurityCerts") AS cert
                WHERE btrim(cert) <> ''
                ON CONFLICT ("Code") DO NOTHING;
                """);

            migrationBuilder.Sql("""
                INSERT INTO vendor_certificates ("VendorId", "CertificateId")
                SELECT DISTINCT vendor."Id", certificate."Id"
                FROM vendors vendor
                CROSS JOIN LATERAL unnest(vendor."SecurityCerts") AS cert
                JOIN certificates certificate ON certificate."Code" = upper(btrim(cert))
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.DropColumn(
                name: "SecurityCerts",
                table: "vendors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The default lets the column be added to a populated table; it is dropped again once
            // the codes have been written back, so the column ends up as it was.
            migrationBuilder.AddColumn<List<string>>(
                name: "SecurityCerts",
                table: "vendors",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.Sql("""
                UPDATE vendors vendor
                SET "SecurityCerts" = COALESCE((
                    SELECT array_agg(certificate."Code" ORDER BY certificate."Code")
                    FROM vendor_certificates link
                    JOIN certificates certificate ON certificate."Id" = link."CertificateId"
                    WHERE link."VendorId" = vendor."Id"), '{}'::text[]);
                """);

            migrationBuilder.Sql("""ALTER TABLE vendors ALTER COLUMN "SecurityCerts" DROP DEFAULT;""");

            migrationBuilder.DropTable(
                name: "vendor_certificates");

            migrationBuilder.DropTable(
                name: "certificates");
        }
    }
}
