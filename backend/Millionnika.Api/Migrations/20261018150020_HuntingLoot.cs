using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations
{
    /// <inheritdoc />
    public partial class HuntingLoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_items_EffectAmount",
                table: "items");

            migrationBuilder.AddColumn<long>(
                name: "MaximumCredits",
                table: "hunting_targets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MinimumCredits",
                table: "hunting_targets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CreditsAwarded",
                table: "hunt_fights",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "LootJson",
                table: "hunt_fights",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "hunting_loot_rows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChancePercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                    MaximumQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hunting_loot_rows", x => x.Id);
                    table.CheckConstraint("CK_hunting_loot_rows_Values", "\"ChancePercent\" BETWEEN 0 AND 100 AND \"MinimumQuantity\" BETWEEN 1 AND 100 AND \"MaximumQuantity\" BETWEEN \"MinimumQuantity\" AND 100");
                    table.ForeignKey(
                        name: "FK_hunting_loot_rows_hunting_targets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "hunting_targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hunting_loot_rows_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_items_EffectAmount",
                table: "items",
                sql: "(\"Type\" = 'consumable' AND \"EffectAmount\" > 0) OR (\"Type\" IN ('weapon','armor','ammunition','material') AND \"EffectAmount\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_hunting_targets_CreditRange",
                table: "hunting_targets",
                sql: "\"MinimumCredits\" BETWEEN 0 AND 1000000000000 AND \"MaximumCredits\" BETWEEN \"MinimumCredits\" AND 1000000000000");

            migrationBuilder.CreateIndex(
                name: "IX_hunting_loot_rows_ItemId",
                table: "hunting_loot_rows",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_hunting_loot_rows_TargetId_ItemId",
                table: "hunting_loot_rows",
                columns: new[] { "TargetId", "ItemId" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO items ("Id", "Name", "Description", "Price", "Type", "IsActive", "EffectResource", "EffectAmount",
                    "AllowedSlots", "MinimumDamage", "MaximumDamage", "MinimumAccuracy", "MaximumAccuracy", "MinimumArmor", "MaximumArmor",
                    "ArmorCoverage", "Caliber", "ClipSize", "RateOfFireMin", "RateOfFireMax", "CooldownSeconds", "CooldownGroup")
                VALUES
                    ('66666666-6666-6666-6666-666666666001', 'Металлолом', 'Детали списанных роботов. Пока только хранится в инвентаре.', 0, 'material', FALSE, '', 0, '', 0, 0, 0, 0, 0, 0, '{}', '', 0, 0, 0, 0, ''),
                    ('66666666-6666-6666-6666-666666666002', 'Компоненты', 'Исправные узлы роботов. Пока только хранится в инвентаре.', 0, 'material', FALSE, '', 0, '', 0, 0, 0, 0, 0, 0, '{}', '', 0, 0, 0, 0, '')
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO hunting_loot_rows ("Id", "TargetId", "ItemId", "ChancePercent", "MinimumQuantity", "MaximumQuantity")
                SELECT v."Id"::uuid, '55555555-5555-5555-5555-555555555002'::uuid, v."ItemId"::uuid,
                    v."ChancePercent", v."MinimumQuantity", v."MaximumQuantity"
                FROM (VALUES
                    ('77777777-7777-7777-7777-777777777001', '66666666-6666-6666-6666-666666666001', 60.000, 1, 2),
                    ('77777777-7777-7777-7777-777777777002', '66666666-6666-6666-6666-666666666002', 15.000, 1, 1),
                    ('77777777-7777-7777-7777-777777777003', '22222222-2222-2222-2222-222222222002', 0.001, 1, 1)
                ) AS v("Id", "ItemId", "ChancePercent", "MinimumQuantity", "MaximumQuantity")
                WHERE EXISTS (SELECT 1 FROM hunting_targets WHERE "Id"='55555555-5555-5555-5555-555555555002')
                  AND EXISTS (SELECT 1 FROM items WHERE "Id"=v."ItemId"::uuid)
                ON CONFLICT ("TargetId", "ItemId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hunting_loot_rows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_items_EffectAmount",
                table: "items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_hunting_targets_CreditRange",
                table: "hunting_targets");

            migrationBuilder.DropColumn(
                name: "MaximumCredits",
                table: "hunting_targets");

            migrationBuilder.DropColumn(
                name: "MinimumCredits",
                table: "hunting_targets");

            migrationBuilder.DropColumn(
                name: "CreditsAwarded",
                table: "hunt_fights");

            migrationBuilder.DropColumn(
                name: "LootJson",
                table: "hunt_fights");

            // Preserve any owned materials; the old constraint will then stop the downgrade safely.
            migrationBuilder.Sql("""
                DELETE FROM items AS item
                WHERE item."Id" IN ('66666666-6666-6666-6666-666666666001', '66666666-6666-6666-6666-666666666002')
                  AND NOT EXISTS (SELECT 1 FROM player_inventory AS owned WHERE owned."ItemId" = item."Id");
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_items_EffectAmount",
                table: "items",
                sql: "(\"Type\" = 'consumable' AND \"EffectAmount\" > 0) OR (\"Type\" IN ('weapon','armor','ammunition') AND \"EffectAmount\" = 0)");
        }
    }
}
