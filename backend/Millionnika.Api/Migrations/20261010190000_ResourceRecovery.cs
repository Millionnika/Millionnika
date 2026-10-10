using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations;

public partial class ResourceRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastEnergyRecoveryAtUtc",
            table: "characters",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "CURRENT_TIMESTAMP");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastNerveRecoveryAtUtc",
            table: "characters",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "CURRENT_TIMESTAMP");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LastEnergyRecoveryAtUtc", table: "characters");
        migrationBuilder.DropColumn(name: "LastNerveRecoveryAtUtc", table: "characters");
    }
}
