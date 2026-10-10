using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations;

public partial class AdminMvp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsBlocked", table: "users", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(
            name: "Role", table: "users", type: "character varying(16)", maxLength: 16,
            nullable: false, defaultValue: "Player");

        migrationBuilder.CreateTable(
            name: "admin_audit_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                TargetType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                TargetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                OldValue = table.Column<string>(type: "jsonb", nullable: false),
                NewValue = table.Column<string>(type: "jsonb", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_audit_logs", x => x.Id);
                table.ForeignKey(
                    name: "FK_admin_audit_logs_users_AdminUserId",
                    column: x => x.AdminUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_admin_audit_logs_AdminUserId", table: "admin_audit_logs", column: "AdminUserId");
        migrationBuilder.CreateIndex(
            name: "IX_admin_audit_logs_CreatedAtUtc", table: "admin_audit_logs", column: "CreatedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "admin_audit_logs");
        migrationBuilder.DropColumn(name: "IsBlocked", table: "users");
        migrationBuilder.DropColumn(name: "Role", table: "users");
    }
}
