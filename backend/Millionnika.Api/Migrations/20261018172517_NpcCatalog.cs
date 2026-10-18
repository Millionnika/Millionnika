using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations
{
    /// <inheritdoc />
    public partial class NpcCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FigurePath",
                table: "hunting_targets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortraitPath",
                table: "hunting_targets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "npc_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PortraitPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FigurePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Health = table.Column<int>(type: "integer", nullable: true),
                    Strength = table.Column<int>(type: "integer", nullable: true),
                    Toughness = table.Column<int>(type: "integer", nullable: true),
                    Reflexes = table.Column<int>(type: "integer", nullable: true),
                    Agility = table.Column<int>(type: "integer", nullable: true),
                    WeaponName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    WeaponDamage = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    WeaponAccuracy = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Armor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ArmorCoverage = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_npc_templates", x => x.Id);
                    table.CheckConstraint("CK_npc_templates_Category", "\"Category\" IN ('peaceful','robot','mutant','animal')");
                    table.CheckConstraint("CK_npc_templates_Combat", "\"Category\" = 'peaceful' OR COALESCE((\"Health\" BETWEEN 1 AND 1000000 AND \"Strength\" BETWEEN 1 AND 1000000 AND \"Toughness\" BETWEEN 1 AND 1000000 AND \"Reflexes\" BETWEEN 1 AND 1000000 AND \"Agility\" BETWEEN 1 AND 1000000 AND \"WeaponName\" IS NOT NULL AND \"WeaponName\" <> '' AND \"WeaponDamage\" BETWEEN 1 AND 1000 AND \"WeaponAccuracy\" BETWEEN 0 AND 100 AND \"Armor\" BETWEEN 0 AND 100 AND \"ArmorCoverage\" IS NOT NULL), FALSE)");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "npc_templates");

            migrationBuilder.DropColumn(
                name: "FigurePath",
                table: "hunting_targets");

            migrationBuilder.DropColumn(
                name: "PortraitPath",
                table: "hunting_targets");
        }
    }
}
