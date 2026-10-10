using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations;

public partial class Friends : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "friend_relations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserLowId = table.Column<Guid>(type: "uuid", nullable: false),
                UserHighId = table.Column<Guid>(type: "uuid", nullable: false),
                SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_friend_relations", x => x.Id);
                table.CheckConstraint("CK_friend_relations_DifferentUsers", "\"UserLowId\" <> \"UserHighId\"");
                table.CheckConstraint("CK_friend_relations_Status", "\"Status\" IN ('Pending', 'Accepted')");
                table.ForeignKey("FK_friend_relations_users_RecipientId", x => x.RecipientId, "users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_friend_relations_users_SenderId", x => x.SenderId, "users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_friend_relations_users_UserHighId", x => x.UserHighId, "users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_friend_relations_users_UserLowId", x => x.UserLowId, "users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_friend_relations_RecipientId", "friend_relations", "RecipientId");
        migrationBuilder.CreateIndex("IX_friend_relations_SenderId", "friend_relations", "SenderId");
        migrationBuilder.CreateIndex("IX_friend_relations_UserHighId", "friend_relations", "UserHighId");
        migrationBuilder.CreateIndex("IX_friend_relations_UserLowId_UserHighId", "friend_relations", new[] { "UserLowId", "UserHighId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "friend_relations");
}
