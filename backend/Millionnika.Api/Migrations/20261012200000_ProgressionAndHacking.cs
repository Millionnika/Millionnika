using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261012200000_ProgressionAndHacking")]
public sealed class ProgressionAndHacking : Migration
{
    private const long EconomyMaximum = 1_000_000_000_000;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>("HackingExperience", "characters", "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<int>("HackingLevel", "characters", "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<string>("Direction", "contracts", "character varying(16)", maxLength: 16, nullable: false, defaultValue: "normal");
        migrationBuilder.AddColumn<int>("MinimumLevel", "contracts", "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>("MinimumHackingLevel", "contracts", "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<long>("RewardHackingExperience", "contracts", "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<long>("HackingExperienceAwarded", "action_logs", "bigint", nullable: false, defaultValue: 0L);

        // Existing experience is authoritative. Refuse an automatic downgrade of a stored level.
        migrationBuilder.Sql("""
            DO $progression$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM characters c
                    WHERE c."Level" > (
                        SELECT MAX(level) FROM generate_series(1, 20) AS level
                        WHERE c."Experience" >= 100::bigint * level * (level - 1) / 2)) THEN
                    RAISE EXCEPTION USING ERRCODE = '23514',
                        MESSAGE = 'Existing character level is above the level calculated from experience. Migration stopped without lowering data.';
                END IF;
            END $progression$;

            UPDATE characters c SET "Level" = (
                SELECT MAX(level) FROM generate_series(1, 20) AS level
                WHERE c."Experience" >= 100::bigint * level * (level - 1) / 2);
            """);

        migrationBuilder.DropCheckConstraint("CK_contracts_NumericLimits", "contracts");
        migrationBuilder.DropCheckConstraint("CK_action_logs_Economy", "action_logs");
        migrationBuilder.AddCheckConstraint("CK_characters_Progression", "characters",
            $"\"Level\" BETWEEN 1 AND 20 AND \"HackingLevel\" BETWEEN 1 AND 20 AND \"HackingExperience\" BETWEEN 0 AND {EconomyMaximum}");
        migrationBuilder.AddCheckConstraint("CK_contracts_NumericLimits", "contracts",
            $"\"CostNerve\" > 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardExperience\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardHackingExperience\" BETWEEN 0 AND {EconomyMaximum}");
        migrationBuilder.AddCheckConstraint("CK_contracts_Progression", "contracts",
            "\"Direction\" IN ('normal', 'hacking') AND \"MinimumLevel\" BETWEEN 1 AND 20 AND \"MinimumHackingLevel\" BETWEEN 1 AND 20 AND (\"Direction\" = 'hacking' OR (\"MinimumHackingLevel\" = 1 AND \"RewardHackingExperience\" = 0))");
        migrationBuilder.AddCheckConstraint("CK_action_logs_Economy", "action_logs",
            $"\"CreditsAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"ExperienceAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"HackingExperienceAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"CreditsSpent\" BETWEEN 0 AND {EconomyMaximum}");

        migrationBuilder.Sql("""
            INSERT INTO contracts ("Id", "Name", "Description", "CostNerve", "SuccessChance",
                "RewardCredits", "RewardExperience", "CooldownSeconds", "IsActive", "Direction",
                "MinimumLevel", "MinimumHackingLevel", "RewardHackingExperience") VALUES
            ('1c92d38c-6aca-4d33-9044-a4a92f6a6201', 'Перепрошить кофейный автомат',
             'Кофейный ИИ объявил себя баристой-провидцем. Вернём ему заводские сомнения.', 1, 0.85, 10, 5, 0, TRUE, 'hacking', 1, 1, 10),
            ('1c92d38c-6aca-4d33-9044-a4a92f6a6202', 'Освободить рекламный терминал',
             'Терминал устал продавать счастье по подписке. Поможем ему показать цифровой закат.', 2, 0.75, 35, 10, 0, TRUE, 'hacking', 1, 2, 15),
            ('1c92d38c-6aca-4d33-9044-a4a92f6a6203', 'Проникнуть в корпоративный архив',
             'В архиве пылится компромат и один рецепт идеальной лапши. Корпорация боится второго.', 3, 0.65, 70, 15, 0, TRUE, 'hacking', 1, 3, 20)
            ON CONFLICT ("Id") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM contracts WHERE \"Id\" IN ('1c92d38c-6aca-4d33-9044-a4a92f6a6201','1c92d38c-6aca-4d33-9044-a4a92f6a6202','1c92d38c-6aca-4d33-9044-a4a92f6a6203');");
        migrationBuilder.DropCheckConstraint("CK_characters_Progression", "characters");
        migrationBuilder.DropCheckConstraint("CK_contracts_Progression", "contracts");
        migrationBuilder.DropCheckConstraint("CK_contracts_NumericLimits", "contracts");
        migrationBuilder.DropCheckConstraint("CK_action_logs_Economy", "action_logs");
        migrationBuilder.DropColumn("HackingExperienceAwarded", "action_logs");
        migrationBuilder.DropColumn("RewardHackingExperience", "contracts");
        migrationBuilder.DropColumn("MinimumHackingLevel", "contracts");
        migrationBuilder.DropColumn("MinimumLevel", "contracts");
        migrationBuilder.DropColumn("Direction", "contracts");
        migrationBuilder.DropColumn("HackingLevel", "characters");
        migrationBuilder.DropColumn("HackingExperience", "characters");
        migrationBuilder.AddCheckConstraint("CK_contracts_NumericLimits", "contracts",
            $"\"CostNerve\" > 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardExperience\" BETWEEN 0 AND {EconomyMaximum}");
        migrationBuilder.AddCheckConstraint("CK_action_logs_Economy", "action_logs",
            $"\"CreditsAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"ExperienceAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"CreditsSpent\" BETWEEN 0 AND {EconomyMaximum}");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        ContractCatalog.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.GameCharacter", entity =>
        {
            entity.Property<long>("HackingExperience").HasColumnType("bigint");
            entity.Property<int>("HackingLevel").HasColumnType("integer");
            entity.ToTable("characters", table => table.HasCheckConstraint("CK_characters_Progression",
                $"\"Level\" BETWEEN 1 AND 20 AND \"HackingLevel\" BETWEEN 1 AND 20 AND \"HackingExperience\" BETWEEN 0 AND {EconomyMaximum}"));
        });
        modelBuilder.Entity("Millionnika.Api.Entities.Contract", entity =>
        {
            entity.Property<string>("Direction").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            entity.Property<int>("MinimumLevel").HasColumnType("integer");
            entity.Property<int>("MinimumHackingLevel").HasColumnType("integer");
            entity.Property<long>("RewardHackingExperience").HasColumnType("bigint");
            entity.ToTable("contracts", table =>
            {
                table.HasCheckConstraint("CK_contracts_NumericLimits", $"\"CostNerve\" > 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardExperience\" BETWEEN 0 AND {EconomyMaximum} AND \"RewardHackingExperience\" BETWEEN 0 AND {EconomyMaximum}");
                table.HasCheckConstraint("CK_contracts_Progression", "\"Direction\" IN ('normal', 'hacking') AND \"MinimumLevel\" BETWEEN 1 AND 20 AND \"MinimumHackingLevel\" BETWEEN 1 AND 20 AND (\"Direction\" = 'hacking' OR (\"MinimumHackingLevel\" = 1 AND \"RewardHackingExperience\" = 0))");
            });
        });
        modelBuilder.Entity("Millionnika.Api.Entities.ActionLog", entity =>
        {
            entity.Property<long>("HackingExperienceAwarded").HasColumnType("bigint");
            entity.ToTable("action_logs", table => table.HasCheckConstraint("CK_action_logs_Economy",
                $"\"CreditsAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"ExperienceAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"HackingExperienceAwarded\" BETWEEN 0 AND {EconomyMaximum} AND \"CreditsSpent\" BETWEEN 0 AND {EconomyMaximum}"));
        });
    }
}
