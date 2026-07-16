using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecimenCheckIn.Context.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "SpecimenCheckIn");

            // Created before the tables, because every tenant-owned LabId column
            // defaults to it. Returns NULL when no lab is in session context, which
            // makes the NOT NULL column reject the insert — writing tenant data
            // without a tenant fails closed rather than landing somewhere arbitrary.
            migrationBuilder.Sql("""
                CREATE FUNCTION [SpecimenCheckIn].[fn_CurrentLabId]()
                RETURNS int
                WITH SCHEMABINDING
                AS
                BEGIN
                    RETURN TRY_CAST(SESSION_CONTEXT(N'LabId') AS int);
                END
                """);

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "SpecimenCheckIn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecimenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClusterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabId = table.Column<int>(type: "int", nullable: false, defaultValueSql: "[SpecimenCheckIn].[fn_CurrentLabId]()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                });

            migrationBuilder.CreateTable(
                name: "Labs",
                schema: "SpecimenCheckIn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Manifests",
                schema: "SpecimenCheckIn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OriginClinic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ClusterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabId = table.Column<int>(type: "int", nullable: false, defaultValueSql: "[SpecimenCheckIn].[fn_CurrentLabId]()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Manifests", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                });

            migrationBuilder.CreateTable(
                name: "Specimens",
                schema: "SpecimenCheckIn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Patient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReceivedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClusterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabId = table.Column<int>(type: "int", nullable: false, defaultValueSql: "[SpecimenCheckIn].[fn_CurrentLabId]()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specimens", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_Specimens_Manifests_ManifestId",
                        column: x => x.ManifestId,
                        principalSchema: "SpecimenCheckIn",
                        principalTable: "Manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Discrepancies",
                schema: "SpecimenCheckIn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecimenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClusterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabId = table.Column<int>(type: "int", nullable: false, defaultValueSql: "[SpecimenCheckIn].[fn_CurrentLabId]()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discrepancies", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_Discrepancies_Manifests_ManifestId",
                        column: x => x.ManifestId,
                        principalSchema: "SpecimenCheckIn",
                        principalTable: "Manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Discrepancies_Specimens_SpecimenId",
                        column: x => x.SpecimenId,
                        principalSchema: "SpecimenCheckIn",
                        principalTable: "Specimens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ClusterId",
                schema: "SpecimenCheckIn",
                table: "AuditEvents",
                column: "ClusterId",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ManifestId_At",
                schema: "SpecimenCheckIn",
                table: "AuditEvents",
                columns: new[] { "ManifestId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_ClusterId",
                schema: "SpecimenCheckIn",
                table: "Discrepancies",
                column: "ClusterId",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_ManifestId",
                schema: "SpecimenCheckIn",
                table: "Discrepancies",
                column: "ManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_SpecimenId",
                schema: "SpecimenCheckIn",
                table: "Discrepancies",
                column: "SpecimenId");

            migrationBuilder.CreateIndex(
                name: "IX_Manifests_ClusterId",
                schema: "SpecimenCheckIn",
                table: "Manifests",
                column: "ClusterId",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_Manifests_LabId_Code",
                schema: "SpecimenCheckIn",
                table: "Manifests",
                columns: new[] { "LabId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_ClusterId",
                schema: "SpecimenCheckIn",
                table: "Specimens",
                column: "ClusterId",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_ManifestId_Code",
                schema: "SpecimenCheckIn",
                table: "Specimens",
                columns: new[] { "ManifestId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "SpecimenCheckIn");

            migrationBuilder.DropTable(
                name: "Discrepancies",
                schema: "SpecimenCheckIn");

            migrationBuilder.DropTable(
                name: "Labs",
                schema: "SpecimenCheckIn");

            migrationBuilder.DropTable(
                name: "Specimens",
                schema: "SpecimenCheckIn");

            migrationBuilder.DropTable(
                name: "Manifests",
                schema: "SpecimenCheckIn");

            // Dropped last: the tables' default constraints depend on it.
            migrationBuilder.Sql("DROP FUNCTION [SpecimenCheckIn].[fn_CurrentLabId]");
        }
    }
}
