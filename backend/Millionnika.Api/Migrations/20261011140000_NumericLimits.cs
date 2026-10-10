using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261011140000_NumericLimits")]
public sealed class NumericLimits : Migration
{
    private const long EconomyMaximum = 1_000_000_000_000;
    private const int StatMaximum = 1_000_000;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Abort before creating any constraint if legacy data violates a new limit.
        // Operations must inspect and correct such data explicitly; this migration never rewrites it.
        migrationBuilder.Sql($$"""
            DO $numeric_limits$
            BEGIN
                IF EXISTS (SELECT 1 FROM characters WHERE "Credits" NOT BETWEEN 0 AND {{EconomyMaximum}}
                    OR "Experience" NOT BETWEEN 0 AND {{EconomyMaximum}}
                    OR "Strength" NOT BETWEEN 0 AND {{StatMaximum}} OR "Speed" NOT BETWEEN 0 AND {{StatMaximum}}
                    OR "Defense" NOT BETWEEN 0 AND {{StatMaximum}} OR "Dexterity" NOT BETWEEN 0 AND {{StatMaximum}})
                OR EXISTS (SELECT 1 FROM contracts WHERE "CostNerve" < 0 OR "SuccessChance" NOT BETWEEN 0 AND 1
                    OR "RewardCredits" NOT BETWEEN 0 AND {{EconomyMaximum}}
                    OR "RewardExperience" NOT BETWEEN 0 AND {{EconomyMaximum}})
                OR EXISTS (SELECT 1 FROM items WHERE "Price" NOT BETWEEN 0 AND {{EconomyMaximum}})
                OR EXISTS (SELECT 1 FROM action_logs WHERE "CreditsAwarded" NOT BETWEEN 0 AND {{EconomyMaximum}}
                    OR "ExperienceAwarded" NOT BETWEEN 0 AND {{EconomyMaximum}}
                    OR "CreditsSpent" NOT BETWEEN 0 AND {{EconomyMaximum}}
                    OR "StatGain" NOT BETWEEN 0 AND {{StatMaximum}}) THEN
                    RAISE EXCEPTION USING ERRCODE = '23514',
                        MESSAGE = 'Existing game data violates NumericLimits. Inspect and correct it explicitly before applying this migration.';
                END IF;
            END $numeric_limits$;
            """);

        migrationBuilder.DropCheckConstraint("CK_items_Price", "items");
        migrationBuilder.AddCheckConstraint("CK_characters_Credits", "characters", $"\"Credits\" BETWEEN 0 AND {EconomyMaximum}");
        migrationBuilder.AddCheckConstraint("CK_characters_Experience", "characters", $"\"Experience\" BETWEEN 0 AND {EconomyMaximum}");
        migrationBuilder.AddCheckConstraint("CK_characters_Stats", "characters", $"\"Strength\" BETWEEN 0 AND {StatMaximum} AND \"Speed\" BETWEEN 0 AND {StatMaximum} AND \"Defense\" BETWEEN 0 AND {StatMaximum} AND \"Dexterity\" BETWEEN 0 AND {StatMaximum}");
        migrationBuilder.AddCheckConstraint("CK_contracts_NumericLimits", "contracts", $"\"CostNerve\" >= 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardExperience\" BETWEEN 0 AND {EconomyMaximum}");
        migrationBuilder.AddCheckConstraint("CK_items_Price", "items", $"\"Price\" BETWEEN 0 AND {EconomyMaximum}");
        migrationBuilder.AddCheckConstraint("CK_action_logs_Economy", "action_logs", $"\"CreditsAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"ExperienceAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"CreditsSpent\" BETWEEN 0 AND {EconomyMaximum}");
        migrationBuilder.AddCheckConstraint("CK_action_logs_StatGain", "action_logs", $"\"StatGain\" BETWEEN 0 AND {StatMaximum}");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_characters_Credits", "characters");
        migrationBuilder.DropCheckConstraint("CK_characters_Experience", "characters");
        migrationBuilder.DropCheckConstraint("CK_characters_Stats", "characters");
        migrationBuilder.DropCheckConstraint("CK_contracts_NumericLimits", "contracts");
        migrationBuilder.DropCheckConstraint("CK_items_Price", "items");
        migrationBuilder.DropCheckConstraint("CK_action_logs_Economy", "action_logs");
        migrationBuilder.DropCheckConstraint("CK_action_logs_StatGain", "action_logs");
        migrationBuilder.AddCheckConstraint("CK_items_Price", "items", "\"Price\" >= 0");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        SecuritySessions.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.GameCharacter")
            .ToTable("characters", table =>
            {
                table.HasCheckConstraint("CK_characters_Credits", $"\"Credits\" BETWEEN 0 AND {EconomyMaximum}");
                table.HasCheckConstraint("CK_characters_Experience", $"\"Experience\" BETWEEN 0 AND {EconomyMaximum}");
                table.HasCheckConstraint("CK_characters_Stats", $"\"Strength\" BETWEEN 0 AND {StatMaximum} AND \"Speed\" BETWEEN 0 AND {StatMaximum} AND \"Defense\" BETWEEN 0 AND {StatMaximum} AND \"Dexterity\" BETWEEN 0 AND {StatMaximum}");
            });
        modelBuilder.Entity("Millionnika.Api.Entities.Contract")
            .ToTable("contracts", table => table.HasCheckConstraint("CK_contracts_NumericLimits",
                $"\"CostNerve\" >= 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardExperience\" BETWEEN 0 AND {EconomyMaximum}"));
        modelBuilder.Entity("Millionnika.Api.Entities.Item")
            .ToTable("items", table =>
            {
                table.HasCheckConstraint("CK_items_EffectAmount", "\"EffectAmount\" > 0");
                table.HasCheckConstraint("CK_items_Price", $"\"Price\" BETWEEN 0 AND {EconomyMaximum}");
            });
        modelBuilder.Entity("Millionnika.Api.Entities.ActionLog")
            .ToTable("action_logs", table =>
            {
                table.HasCheckConstraint("CK_action_logs_Economy", $"\"CreditsAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"ExperienceAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"CreditsSpent\" BETWEEN 0 AND {EconomyMaximum}");
                table.HasCheckConstraint("CK_action_logs_StatGain", $"\"StatGain\" BETWEEN 0 AND {StatMaximum}");
            });
    }
}
