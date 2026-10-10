using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations;

public partial class GameplayActions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "contracts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                CostNerve = table.Column<int>(type: "integer", nullable: false),
                SuccessChance = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                RewardCredits = table.Column<long>(type: "bigint", nullable: false),
                RewardExperience = table.Column<long>(type: "bigint", nullable: false),
                CooldownSeconds = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_contracts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "action_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                ActionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                ErrorCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                Message = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                EnergySpent = table.Column<int>(type: "integer", nullable: false),
                NerveSpent = table.Column<int>(type: "integer", nullable: false),
                CreditsAwarded = table.Column<long>(type: "bigint", nullable: false),
                ExperienceAwarded = table.Column<long>(type: "bigint", nullable: false),
                StatName = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                StatGain = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_action_logs", x => x.Id);
                table.ForeignKey(
                    name: "FK_action_logs_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_action_logs_UserId_RequestId",
            table: "action_logs",
            columns: new[] { "UserId", "RequestId" },
            unique: true);

        migrationBuilder.InsertData(
            table: "contracts",
            columns: new[] { "Id", "Name", "CostNerve", "SuccessChance", "RewardCredits", "RewardExperience", "CooldownSeconds", "IsActive" },
            values: new object[]
            {
                new Guid("1c92d38c-6aca-4d33-9044-a4a92f6a6101"),
                "Перехват рекламного дрона",
                2,
                0.8m,
                50L,
                10L,
                0,
                true
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "action_logs");
        migrationBuilder.DropTable(name: "contracts");
    }
}
