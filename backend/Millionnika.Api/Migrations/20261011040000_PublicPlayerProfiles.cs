using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations;

public partial class PublicPlayerProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "users",
            type: "character varying(300)",
            maxLength: 300,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Description", table: "users");
    }
}
