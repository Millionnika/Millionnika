using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations;

public partial class InitialAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Username = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                NormalizedUsername = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "characters",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Level = table.Column<int>(type: "integer", nullable: false),
                Credits = table.Column<long>(type: "bigint", nullable: false),
                Energy = table.Column<int>(type: "integer", nullable: false),
                MaxEnergy = table.Column<int>(type: "integer", nullable: false),
                Nerve = table.Column<int>(type: "integer", nullable: false),
                MaxNerve = table.Column<int>(type: "integer", nullable: false),
                Health = table.Column<int>(type: "integer", nullable: false),
                MaxHealth = table.Column<int>(type: "integer", nullable: false),
                Strength = table.Column<int>(type: "integer", nullable: false),
                Speed = table.Column<int>(type: "integer", nullable: false),
                Defense = table.Column<int>(type: "integer", nullable: false),
                Dexterity = table.Column<int>(type: "integer", nullable: false),
                Experience = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_characters", x => x.Id);
                table.ForeignKey(
                    name: "FK_characters_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_characters_UserId",
            table: "characters",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_NormalizedEmail",
            table: "users",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_NormalizedUsername",
            table: "users",
            column: "NormalizedUsername",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "characters");
        migrationBuilder.DropTable(name: "users");
    }
}
