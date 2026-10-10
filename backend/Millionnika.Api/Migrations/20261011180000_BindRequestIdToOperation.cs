using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261011180000_BindRequestIdToOperation")]
public sealed class BindRequestIdToOperation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequestSignature",
            table: "action_logs",
            type: "character varying(160)",
            maxLength: 160,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE action_logs
            SET "RequestSignature" = CASE
                WHEN "ActionType" = 'contract' THEN 'contract:v1'
                WHEN "ActionType" = 'training' THEN 'training:v1:' || lower(coalesce("StatName", ''))
                WHEN "ActionType" IN ('shop_purchase', 'item_use') THEN
                    "ActionType" || ':v1:' || lower(coalesce("ItemId"::text, '')) || ':' || "ItemQuantity"::text
                ELSE 'legacy:v1:' || lower("ActionType")
            END;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "CK_action_logs_RequestSignature",
            table: "action_logs",
            sql: "\"RequestSignature\" <> ''");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_action_logs_RequestSignature", "action_logs");
        migrationBuilder.DropColumn("RequestSignature", "action_logs");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        PositiveContractCost.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.ActionLog", entity =>
        {
            entity.Property<string>("RequestSignature")
                .IsRequired()
                .HasMaxLength(160)
                .HasColumnType("character varying(160)");
            entity.ToTable("action_logs", table =>
                table.HasCheckConstraint("CK_action_logs_RequestSignature", "\"RequestSignature\" <> ''"));
        });
    }
}
