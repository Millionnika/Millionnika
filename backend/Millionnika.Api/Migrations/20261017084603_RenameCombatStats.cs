using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameCombatStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_hunting_targets_Values",
                table: "hunting_targets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_characters_Stats",
                table: "characters");

            migrationBuilder.RenameColumn(
                name: "Speed",
                table: "hunting_targets",
                newName: "Reflexes");

            migrationBuilder.RenameColumn(
                name: "Dexterity",
                table: "hunting_targets",
                newName: "Agility");

            migrationBuilder.RenameColumn(
                name: "Defense",
                table: "hunting_targets",
                newName: "Toughness");

            migrationBuilder.RenameColumn(
                name: "Speed",
                table: "characters",
                newName: "Reflexes");

            migrationBuilder.RenameColumn(
                name: "Dexterity",
                table: "characters",
                newName: "Agility");

            migrationBuilder.RenameColumn(
                name: "Defense",
                table: "characters",
                newName: "Toughness");

            migrationBuilder.Sql("""
                UPDATE hunt_fights
                SET "SnapshotJson" = jsonb_set(
                    "SnapshotJson",
                    '{playerStats}',
                    (("SnapshotJson" -> 'playerStats') - 'speed' - 'defense' - 'dexterity') ||
                    jsonb_build_object(
                        'reflexes', "SnapshotJson" #> '{playerStats,speed}',
                        'toughness', "SnapshotJson" #> '{playerStats,defense}',
                        'agility', "SnapshotJson" #> '{playerStats,dexterity}'))
                WHERE ("SnapshotJson" -> 'playerStats') ? 'speed';

                UPDATE hunt_fights
                SET "SnapshotJson" = jsonb_set(
                    "SnapshotJson",
                    '{robotStats}',
                    (("SnapshotJson" -> 'robotStats') - 'speed' - 'defense' - 'dexterity') ||
                    jsonb_build_object(
                        'reflexes', "SnapshotJson" #> '{robotStats,speed}',
                        'toughness', "SnapshotJson" #> '{robotStats,defense}',
                        'agility', "SnapshotJson" #> '{robotStats,dexterity}'))
                WHERE ("SnapshotJson" -> 'robotStats') ? 'speed';

                UPDATE action_logs SET "StatName" = 'Reflexes' WHERE "StatName" = 'Speed';
                UPDATE action_logs SET "StatName" = 'Toughness' WHERE "StatName" = 'Defense';
                UPDATE action_logs SET "StatName" = 'Agility' WHERE "StatName" = 'Dexterity';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_hunting_targets_Values",
                table: "hunting_targets",
                sql: "\"EnergyCost\" > 0 AND \"Health\" > 0 AND \"Strength\" > 0 AND \"Reflexes\" > 0 AND \"Toughness\" > 0 AND \"Agility\" > 0 AND \"RewardCredits\" BETWEEN 0 AND 1000000000000 AND \"RewardExperience\" BETWEEN 0 AND 1000000000000 AND \"RewardHuntingExperience\" BETWEEN 0 AND 1000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_characters_Stats",
                table: "characters",
                sql: "\"Strength\" BETWEEN 0 AND 1000000 AND \"Reflexes\" BETWEEN 0 AND 1000000 AND \"Toughness\" BETWEEN 0 AND 1000000 AND \"Agility\" BETWEEN 0 AND 1000000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_hunting_targets_Values",
                table: "hunting_targets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_characters_Stats",
                table: "characters");

            migrationBuilder.RenameColumn(
                name: "Reflexes",
                table: "hunting_targets",
                newName: "Speed");

            migrationBuilder.RenameColumn(
                name: "Agility",
                table: "hunting_targets",
                newName: "Dexterity");

            migrationBuilder.RenameColumn(
                name: "Toughness",
                table: "hunting_targets",
                newName: "Defense");

            migrationBuilder.RenameColumn(
                name: "Reflexes",
                table: "characters",
                newName: "Speed");

            migrationBuilder.RenameColumn(
                name: "Agility",
                table: "characters",
                newName: "Dexterity");

            migrationBuilder.RenameColumn(
                name: "Toughness",
                table: "characters",
                newName: "Defense");

            migrationBuilder.Sql("""
                UPDATE hunt_fights
                SET "SnapshotJson" = jsonb_set(
                    "SnapshotJson",
                    '{playerStats}',
                    (("SnapshotJson" -> 'playerStats') - 'reflexes' - 'toughness' - 'agility') ||
                    jsonb_build_object(
                        'speed', "SnapshotJson" #> '{playerStats,reflexes}',
                        'defense', "SnapshotJson" #> '{playerStats,toughness}',
                        'dexterity', "SnapshotJson" #> '{playerStats,agility}'))
                WHERE ("SnapshotJson" -> 'playerStats') ? 'reflexes';

                UPDATE hunt_fights
                SET "SnapshotJson" = jsonb_set(
                    "SnapshotJson",
                    '{robotStats}',
                    (("SnapshotJson" -> 'robotStats') - 'reflexes' - 'toughness' - 'agility') ||
                    jsonb_build_object(
                        'speed', "SnapshotJson" #> '{robotStats,reflexes}',
                        'defense', "SnapshotJson" #> '{robotStats,toughness}',
                        'dexterity', "SnapshotJson" #> '{robotStats,agility}'))
                WHERE ("SnapshotJson" -> 'robotStats') ? 'reflexes';

                UPDATE action_logs SET "StatName" = 'Speed' WHERE "StatName" = 'Reflexes';
                UPDATE action_logs SET "StatName" = 'Defense' WHERE "StatName" = 'Toughness';
                UPDATE action_logs SET "StatName" = 'Dexterity' WHERE "StatName" = 'Agility';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_hunting_targets_Values",
                table: "hunting_targets",
                sql: "\"EnergyCost\" > 0 AND \"Health\" > 0 AND \"Strength\" > 0 AND \"Speed\" > 0 AND \"Defense\" > 0 AND \"Dexterity\" > 0 AND \"RewardCredits\" BETWEEN 0 AND 1000000000000 AND \"RewardExperience\" BETWEEN 0 AND 1000000000000 AND \"RewardHuntingExperience\" BETWEEN 0 AND 1000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_characters_Stats",
                table: "characters",
                sql: "\"Strength\" BETWEEN 0 AND 1000000 AND \"Speed\" BETWEEN 0 AND 1000000 AND \"Defense\" BETWEEN 0 AND 1000000 AND \"Dexterity\" BETWEEN 0 AND 1000000");
        }
    }
}
