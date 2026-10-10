using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations
{
    /// <inheritdoc />
    public partial class RobotHunting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_items_EffectAmount",
                table: "items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_characters_Progression",
                table: "characters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_action_logs_Economy",
                table: "action_logs");

            migrationBuilder.AddColumn<string>(
                name: "Caliber",
                table: "items",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ClipSize",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RateOfFireMax",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RateOfFireMin",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "HuntingExperience",
                table: "characters",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "HuntingLevel",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "HuntingExperienceAwarded",
                table: "action_logs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "hunting_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MinimumHuntingLevel = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hunting_locations", x => x.Id);
                    table.CheckConstraint("CK_hunting_locations_Level", "\"MinimumHuntingLevel\" BETWEEN 1 AND 20");
                });

            migrationBuilder.CreateTable(
                name: "hunting_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Signature = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hunting_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hunting_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hunting_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MinimumHuntingLevel = table.Column<int>(type: "integer", nullable: false),
                    EnergyCost = table.Column<int>(type: "integer", nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    Strength = table.Column<int>(type: "integer", nullable: false),
                    Speed = table.Column<int>(type: "integer", nullable: false),
                    Defense = table.Column<int>(type: "integer", nullable: false),
                    Dexterity = table.Column<int>(type: "integer", nullable: false),
                    WeaponName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WeaponDamage = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    WeaponAccuracy = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ClipSize = table.Column<int>(type: "integer", nullable: false),
                    RateOfFireMin = table.Column<int>(type: "integer", nullable: false),
                    RateOfFireMax = table.Column<int>(type: "integer", nullable: false),
                    SpareClips = table.Column<int>(type: "integer", nullable: false),
                    Armor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ArmorCoverage = table.Column<string>(type: "jsonb", nullable: false),
                    RewardCredits = table.Column<long>(type: "bigint", nullable: false),
                    RewardExperience = table.Column<long>(type: "bigint", nullable: false),
                    RewardHuntingExperience = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hunting_targets", x => x.Id);
                    table.CheckConstraint("CK_hunting_targets_Level", "\"MinimumHuntingLevel\" BETWEEN 1 AND 20");
                    table.CheckConstraint("CK_hunting_targets_Values", "\"EnergyCost\" > 0 AND \"Health\" > 0 AND \"Strength\" > 0 AND \"Speed\" > 0 AND \"Defense\" > 0 AND \"Dexterity\" > 0 AND \"RewardCredits\" BETWEEN 0 AND 1000000000000 AND \"RewardExperience\" BETWEEN 0 AND 1000000000000 AND \"RewardHuntingExperience\" BETWEEN 0 AND 1000000000000");
                    table.CheckConstraint("CK_hunting_targets_Weapon", "\"WeaponDamage\" BETWEEN 1 AND 1000 AND \"WeaponAccuracy\" BETWEEN 0 AND 100 AND \"ClipSize\" BETWEEN 0 AND 1000 AND \"RateOfFireMin\" BETWEEN 0 AND 1000 AND \"RateOfFireMax\" BETWEEN \"RateOfFireMin\" AND 1000 AND \"SpareClips\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_hunting_targets_hunting_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "hunting_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hunt_fights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Turn = table.Column<int>(type: "integer", nullable: false),
                    PlayerHealth = table.Column<int>(type: "integer", nullable: false),
                    RobotHealth = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    StateJson = table.Column<string>(type: "jsonb", nullable: false),
                    LogJson = table.Column<string>(type: "jsonb", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActionAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizationApplied = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hunt_fights", x => x.Id);
                    table.CheckConstraint("CK_hunt_fights_Status", "\"Status\" IN ('active','won','lost','stalemate','timeout')");
                    table.CheckConstraint("CK_hunt_fights_Values", "\"Turn\" BETWEEN 0 AND 25 AND \"PlayerHealth\" >= 0 AND \"RobotHealth\" >= 0");
                    table.ForeignKey(
                        name: "FK_hunt_fights_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hunt_fights_hunting_targets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "hunting_targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111001"),
                columns: new[] { "Caliber", "ClipSize", "RateOfFireMax", "RateOfFireMin" },
                values: new object[] { "", 0, 0, 0 });

            migrationBuilder.Sql("""
                UPDATE items SET "Caliber"='pulse-cell', "ClipSize"=30, "RateOfFireMin"=3, "RateOfFireMax"=5
                WHERE "Id"='22222222-2222-2222-2222-222222222001';
                UPDATE items SET "Caliber"='pulse-cell', "ClipSize"=12, "RateOfFireMin"=1, "RateOfFireMax"=2
                WHERE "Id"='22222222-2222-2222-2222-222222222002';

                INSERT INTO items ("Id","Name","Description","Price","Type","IsActive","EffectResource","EffectAmount","AllowedSlots","MinimumDamage","MaximumDamage","MinimumAccuracy","MaximumAccuracy","MinimumArmor","MaximumArmor","ArmorCoverage","Caliber","ClipSize","RateOfFireMin","RateOfFireMax")
                VALUES ('33333333-3333-3333-3333-333333333001','Стандартный импульсный заряд','Обычный заряд к импульсному оружию. Продаётся поштучно.',1,'ammunition',TRUE,'',0,'',0,0,0,0,0,0,'{}','pulse-cell',0,0,0)
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO hunting_locations ("Id","Name","Description","MinimumHuntingLevel","IsActive") VALUES
                ('44444444-4444-4444-4444-444444444001','Свалка дронов','Ржавое море, где списанные машины всё ещё спорят с гарантией.',1,TRUE),
                ('44444444-4444-4444-4444-444444444002','Заброшенное депо','Депо закрыто, но роботы об этом не читали.',3,TRUE)
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO hunting_targets ("Id","LocationId","Name","Description","MinimumHuntingLevel","EnergyCost","Health","Strength","Speed","Defense","Dexterity","WeaponName","WeaponDamage","WeaponAccuracy","ClipSize","RateOfFireMin","RateOfFireMax","SpareClips","Armor","ArmorCoverage","RewardCredits","RewardExperience","RewardHuntingExperience","IsActive") VALUES
                ('55555555-5555-5555-5555-555555555001','44444444-4444-4444-4444-444444444001','Дрон-сборщик','Собирает металл, штрафы и чужое чувство достоинства.',1,10,30,1,1,1,1,'Промышленный резак',10,50,0,0,0,0,0,'{}',20,5,10,TRUE),
                ('55555555-5555-5555-5555-555555555002','44444444-4444-4444-4444-444444444001','Сторожевой паук','Восемь ног, шесть камер и ни одного выходного.',2,12,55,2,2,2,2,'Импульсный шипомёт',24,48,6,1,2,1,8,'{"chest":100,"stomach":100}',45,8,15,TRUE),
                ('55555555-5555-5555-5555-555555555003','44444444-4444-4444-4444-444444444002','Погрузчик-бунтарь','Профсоюз не одобрил его методы переговоров.',3,15,90,3,3,3,3,'Грузовой импульсник',32,52,8,2,3,2,15,'{"chest":100,"stomach":100,"arms":100,"legs":100}',90,12,25,TRUE)
                ON CONFLICT ("Id") DO NOTHING;
                """);

            migrationBuilder.UpdateData(
                table: "items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111002"),
                columns: new[] { "Caliber", "ClipSize", "RateOfFireMax", "RateOfFireMin" },
                values: new object[] { "", 0, 0, 0 });

            migrationBuilder.UpdateData(
                table: "items",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111003"),
                columns: new[] { "Caliber", "ClipSize", "RateOfFireMax", "RateOfFireMin" },
                values: new object[] { "", 0, 0, 0 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_items_Ammo",
                table: "items",
                sql: "\"ClipSize\" BETWEEN 0 AND 1000 AND \"RateOfFireMin\" BETWEEN 0 AND 1000 AND \"RateOfFireMax\" BETWEEN \"RateOfFireMin\" AND 1000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_items_EffectAmount",
                table: "items",
                sql: "(\"Type\" = 'consumable' AND \"EffectAmount\" > 0) OR (\"Type\" IN ('weapon','armor','ammunition') AND \"EffectAmount\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_characters_Progression",
                table: "characters",
                sql: "\"Level\" BETWEEN 1 AND 20 AND \"HackingLevel\" BETWEEN 1 AND 20 AND \"HackingExperience\" BETWEEN 0 AND 1000000000000 AND \"HuntingLevel\" BETWEEN 1 AND 20 AND \"HuntingExperience\" BETWEEN 0 AND 1000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_action_logs_Economy",
                table: "action_logs",
                sql: "\"CreditsAwarded\" BETWEEN 0 AND 1000000000000 AND \"ExperienceAwarded\" BETWEEN 0 AND 1000000000000 AND \"HackingExperienceAwarded\" BETWEEN 0 AND 1000000000000 AND \"HuntingExperienceAwarded\" BETWEEN 0 AND 1000000000000 AND \"CreditsSpent\" BETWEEN 0 AND 1000000000000");

            migrationBuilder.CreateIndex(
                name: "IX_hunt_fights_CharacterId",
                table: "hunt_fights",
                column: "CharacterId",
                unique: true,
                filter: "\"Status\" = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_hunt_fights_TargetId",
                table: "hunt_fights",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_hunting_requests_UserId_RequestId",
                table: "hunting_requests",
                columns: new[] { "UserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hunting_targets_LocationId",
                table: "hunting_targets",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hunt_fights");

            migrationBuilder.DropTable(
                name: "hunting_requests");

            migrationBuilder.DropTable(
                name: "hunting_targets");

            migrationBuilder.DropTable(
                name: "hunting_locations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_items_Ammo",
                table: "items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_items_EffectAmount",
                table: "items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_characters_Progression",
                table: "characters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_action_logs_Economy",
                table: "action_logs");

            migrationBuilder.Sql("DELETE FROM items WHERE \"Type\"='ammunition';");

            migrationBuilder.DropColumn(
                name: "Caliber",
                table: "items");

            migrationBuilder.DropColumn(
                name: "ClipSize",
                table: "items");

            migrationBuilder.DropColumn(
                name: "RateOfFireMax",
                table: "items");

            migrationBuilder.DropColumn(
                name: "RateOfFireMin",
                table: "items");

            migrationBuilder.DropColumn(
                name: "HuntingExperience",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "HuntingLevel",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "HuntingExperienceAwarded",
                table: "action_logs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_items_EffectAmount",
                table: "items",
                sql: "(\"Type\" = 'consumable' AND \"EffectAmount\" > 0) OR (\"Type\" IN ('weapon','armor') AND \"EffectAmount\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_characters_Progression",
                table: "characters",
                sql: "\"Level\" BETWEEN 1 AND 20 AND \"HackingLevel\" BETWEEN 1 AND 20 AND \"HackingExperience\" BETWEEN 0 AND 1000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_action_logs_Economy",
                table: "action_logs",
                sql: "\"CreditsAwarded\" BETWEEN 0 AND 1000000000000 AND \"ExperienceAwarded\" BETWEEN 0 AND 1000000000000 AND \"HackingExperienceAwarded\" BETWEEN 0 AND 1000000000000 AND \"CreditsSpent\" BETWEEN 0 AND 1000000000000");
        }
    }
}
