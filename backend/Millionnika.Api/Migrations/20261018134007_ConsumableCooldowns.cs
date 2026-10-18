using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConsumableCooldowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CooldownGroup",
                table: "items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CooldownSeconds",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "consumable_cooldowns",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Group = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumable_cooldowns", x => new { x.CharacterId, x.Group });
                    table.ForeignKey(
                        name: "FK_consumable_cooldowns_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_items_Cooldown",
                table: "items",
                sql: "\"CooldownSeconds\" BETWEEN 0 AND 86400 AND ((\"Type\" = 'consumable' AND ((\"CooldownSeconds\" = 0 AND \"CooldownGroup\" = '') OR (\"CooldownSeconds\" > 0 AND \"CooldownGroup\" <> ''))) OR (\"Type\" <> 'consumable' AND \"CooldownSeconds\" = 0 AND \"CooldownGroup\" = ''))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumable_cooldowns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_items_Cooldown",
                table: "items");

            migrationBuilder.DropColumn(
                name: "CooldownGroup",
                table: "items");

            migrationBuilder.DropColumn(
                name: "CooldownSeconds",
                table: "items");
        }
    }
}
