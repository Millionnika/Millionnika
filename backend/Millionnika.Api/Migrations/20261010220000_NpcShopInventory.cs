using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Millionnika.Api.Migrations;

public partial class NpcShopInventory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "CreditsSpent",
            table: "action_logs",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<Guid>(
            name: "ItemId",
            table: "action_logs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ItemQuantity",
            table: "action_logs",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Price = table.Column<long>(type: "bigint", nullable: false),
                Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                EffectResource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                EffectAmount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_items", x => x.Id);
                table.CheckConstraint("CK_items_EffectAmount", "\"EffectAmount\" > 0");
                table.CheckConstraint("CK_items_Price", "\"Price\" >= 0");
            });

        migrationBuilder.CreateTable(
            name: "player_inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_player_inventory", x => x.Id);
                table.CheckConstraint("CK_player_inventory_Quantity", "\"Quantity\" > 0");
                table.ForeignKey(
                    name: "FK_player_inventory_characters_CharacterId",
                    column: x => x.CharacterId,
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_player_inventory_items_ItemId",
                    column: x => x.ItemId,
                    principalTable: "items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            table: "items",
            columns: ["Id", "Description", "EffectAmount", "EffectResource", "IsActive", "Name", "Price", "Type"],
            values: new object[,]
            {
                { Guid.Parse("11111111-1111-1111-1111-111111111001"), "Быстрый нейропитательный батончик. Восстанавливает 10 энергии.", 10, "energy", true, "Нейро-снэк", 25L, "consumable" },
                { Guid.Parse("11111111-1111-1111-1111-111111111002"), "Стабилизирует нервную систему. Восстанавливает 3 хладнокровия.", 3, "nerve", true, "Успокоитель", 20L, "consumable" },
                { Guid.Parse("11111111-1111-1111-1111-111111111003"), "Медицинский наногель. Восстанавливает 20 здоровья.", 20, "health", true, "Аптечный наногель", 35L, "consumable" }
            });

        migrationBuilder.CreateIndex(
            name: "IX_player_inventory_CharacterId_ItemId",
            table: "player_inventory",
            columns: ["CharacterId", "ItemId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_player_inventory_ItemId",
            table: "player_inventory",
            column: "ItemId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "player_inventory");
        migrationBuilder.DropTable(name: "items");
        migrationBuilder.DropColumn(name: "CreditsSpent", table: "action_logs");
        migrationBuilder.DropColumn(name: "ItemId", table: "action_logs");
        migrationBuilder.DropColumn(name: "ItemQuantity", table: "action_logs");
    }
}
