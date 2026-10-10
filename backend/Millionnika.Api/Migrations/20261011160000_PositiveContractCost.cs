using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261011160000_PositiveContractCost")]
public sealed class PositiveContractCost : Migration
{
    private const long EconomyMaximum = 1_000_000_000_000;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $positive_contract_cost$
            BEGIN
                IF EXISTS (SELECT 1 FROM contracts WHERE "CostNerve" <= 0) THEN
                    RAISE EXCEPTION USING ERRCODE = '23514',
                        MESSAGE = 'Existing contract data violates PositiveContractCost. Inspect and correct it explicitly before applying this migration.';
                END IF;
            END $positive_contract_cost$;
            """);
        migrationBuilder.DropCheckConstraint("CK_contracts_NumericLimits", "contracts");
        migrationBuilder.AddCheckConstraint("CK_contracts_NumericLimits", "contracts",
            $"\"CostNerve\" > 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardExperience\" BETWEEN 0 AND {EconomyMaximum}");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_contracts_NumericLimits", "contracts");
        migrationBuilder.AddCheckConstraint("CK_contracts_NumericLimits", "contracts",
            $"\"CostNerve\" >= 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardExperience\" BETWEEN 0 AND {EconomyMaximum}");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        NumericLimits.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.Contract")
            .ToTable("contracts", table => table.HasCheckConstraint("CK_contracts_NumericLimits",
                $"\"CostNerve\" > 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardExperience\" BETWEEN 0 AND {EconomyMaximum}"));
    }
}
