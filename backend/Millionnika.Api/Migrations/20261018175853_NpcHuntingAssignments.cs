using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations
{
    /// <inheritdoc />
    public partial class NpcHuntingAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "hunting_targets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "robot");

            migrationBuilder.CreateTable(
                name: "hunting_location_targets",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hunting_location_targets", x => new { x.LocationId, x.TargetId });
                    table.ForeignKey(
                        name: "FK_hunting_location_targets_hunting_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "hunting_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hunting_location_targets_hunting_targets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "hunting_targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO hunting_location_targets ("LocationId", "TargetId")
                SELECT "LocationId", "Id" FROM hunting_targets;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_hunting_targets_hunting_locations_LocationId",
                table: "hunting_targets");
            migrationBuilder.DropIndex(
                name: "IX_hunting_targets_LocationId",
                table: "hunting_targets");
            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "hunting_targets");

            migrationBuilder.AddCheckConstraint(
                name: "CK_hunting_targets_Category",
                table: "hunting_targets",
                sql: "\"Category\" IN ('robot','mutant','animal')");

            migrationBuilder.CreateIndex(
                name: "IX_hunting_location_targets_TargetId",
                table: "hunting_location_targets",
                column: "TargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM hunting_targets t LEFT JOIN hunting_location_targets a ON a."TargetId" = t."Id"
                        GROUP BY t."Id" HAVING COUNT(a."LocationId") <> 1) THEN
                        RAISE EXCEPTION 'Cannot revert NPC hunting assignments: each target must have exactly one location';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "hunting_targets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE hunting_targets t SET "LocationId" = a."LocationId"
                FROM hunting_location_targets a WHERE a."TargetId" = t."Id";
                """);

            migrationBuilder.DropTable(
                name: "hunting_location_targets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_hunting_targets_Category",
                table: "hunting_targets");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "hunting_targets");

            migrationBuilder.CreateIndex(
                name: "IX_hunting_targets_LocationId",
                table: "hunting_targets",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_hunting_targets_hunting_locations_LocationId",
                table: "hunting_targets",
                column: "LocationId",
                principalTable: "hunting_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
