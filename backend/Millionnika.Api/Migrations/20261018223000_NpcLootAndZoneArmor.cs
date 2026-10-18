using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261018223000_NpcLootAndZoneArmor")]
public sealed class NpcLootAndZoneArmor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("ArmorZones", "hunting_targets", type: "jsonb", nullable: false, defaultValue: "{}");
        migrationBuilder.AddColumn<string>("ArmorZones", "npc_templates", type: "jsonb", nullable: true);
        const string parts = "(VALUES ('head'),('throat'),('heart'),('chest'),('stomach'),('arms'),('hands'),('groin'),('legs'),('feet')) AS z(part)";
        migrationBuilder.Sql($"""
            UPDATE hunting_targets AS t SET "ArmorZones" =
              (SELECT jsonb_object_agg(z.part, jsonb_build_object(
                'coverage', COALESCE((t."ArmorCoverage"->>z.part)::numeric, 0),
                'protection', CASE WHEN t."ArmorCoverage" ? z.part THEN t."Armor" ELSE 0 END)) FROM {parts});
            UPDATE npc_templates AS t SET "ArmorZones" =
              (SELECT jsonb_object_agg(z.part, jsonb_build_object(
                'coverage', COALESCE((t."ArmorCoverage"->>z.part)::numeric, 0),
                'protection', CASE WHEN t."ArmorCoverage" ? z.part THEN t."Armor" ELSE 0 END)) FROM {parts})
              WHERE t."Category" <> 'peaceful' OR t."Health" IS NOT NULL;
            """);
        migrationBuilder.CreateTable(
            name: "npc_template_loot_rows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                NpcId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                ChancePercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                MaximumQuantity = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_npc_template_loot_rows", x => x.Id);
                table.ForeignKey("FK_npc_template_loot_rows_npc_templates_NpcId", x => x.NpcId,
                    "npc_templates", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_npc_template_loot_rows_items_ItemId", x => x.ItemId,
                    "items", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_npc_template_loot_rows_Values",
                    "\"ChancePercent\" BETWEEN 0 AND 100 AND \"MinimumQuantity\" BETWEEN 1 AND 100 AND \"MaximumQuantity\" BETWEEN \"MinimumQuantity\" AND 100");
            });
        migrationBuilder.CreateIndex("IX_npc_template_loot_rows_NpcId_ItemId", "npc_template_loot_rows",
            new[] { "NpcId", "ItemId" }, unique: true);
        migrationBuilder.CreateIndex("IX_npc_template_loot_rows_ItemId", "npc_template_loot_rows", "ItemId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("npc_template_loot_rows");
        migrationBuilder.DropColumn("ArmorZones", "hunting_targets");
        migrationBuilder.DropColumn("ArmorZones", "npc_templates");
    }
}
