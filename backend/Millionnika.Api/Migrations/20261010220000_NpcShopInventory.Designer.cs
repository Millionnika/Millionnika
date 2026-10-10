using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261010220000_NpcShopInventory")]
partial class NpcShopInventory
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
#pragma warning restore 612, 618
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        ResourceRecovery.BuildModel(modelBuilder);

        modelBuilder.Entity("Millionnika.Api.Entities.ActionLog", entity =>
        {
            entity.Property<long>("CreditsSpent").HasColumnType("bigint");
            entity.Property<Guid?>("ItemId").HasColumnType("uuid");
            entity.Property<int>("ItemQuantity").HasColumnType("integer");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.Item", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property<string>("Description").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)");
            entity.Property<int>("EffectAmount").HasColumnType("integer");
            entity.Property<string>("EffectResource").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            entity.Property<bool>("IsActive").HasColumnType("boolean");
            entity.Property<string>("Name").IsRequired().HasMaxLength(120).HasColumnType("character varying(120)");
            entity.Property<long>("Price").HasColumnType("bigint");
            entity.Property<string>("Type").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
            entity.HasKey("Id");
            entity.ToTable("items", table =>
            {
                table.HasCheckConstraint("CK_items_EffectAmount", "\"EffectAmount\" > 0");
                table.HasCheckConstraint("CK_items_Price", "\"Price\" >= 0");
            });
            entity.HasData(
                new { Id = Guid.Parse("11111111-1111-1111-1111-111111111001"), Description = "Быстрый нейропитательный батончик. Восстанавливает 10 энергии.", EffectAmount = 10, EffectResource = "energy", IsActive = true, Name = "Нейро-снэк", Price = 25L, Type = "consumable" },
                new { Id = Guid.Parse("11111111-1111-1111-1111-111111111002"), Description = "Стабилизирует нервную систему. Восстанавливает 3 хладнокровия.", EffectAmount = 3, EffectResource = "nerve", IsActive = true, Name = "Успокоитель", Price = 20L, Type = "consumable" },
                new { Id = Guid.Parse("11111111-1111-1111-1111-111111111003"), Description = "Медицинский наногель. Восстанавливает 20 здоровья.", EffectAmount = 20, EffectResource = "health", IsActive = true, Name = "Аптечный наногель", Price = 35L, Type = "consumable" });
        });

        modelBuilder.Entity("Millionnika.Api.Entities.PlayerInventoryItem", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property<Guid>("CharacterId").HasColumnType("uuid");
            entity.Property<Guid>("ItemId").HasColumnType("uuid");
            entity.Property<int>("Quantity").HasColumnType("integer");
            entity.HasKey("Id");
            entity.HasIndex("CharacterId", "ItemId").IsUnique();
            entity.HasIndex("ItemId");
            entity.ToTable("player_inventory", table =>
                table.HasCheckConstraint("CK_player_inventory_Quantity", "\"Quantity\" > 0"));
        });

        modelBuilder.Entity("Millionnika.Api.Entities.PlayerInventoryItem", entity =>
        {
            entity.HasOne("Millionnika.Api.Entities.GameCharacter", "Character")
                .WithMany("Inventory").HasForeignKey("CharacterId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            entity.HasOne("Millionnika.Api.Entities.Item", "Item")
                .WithMany("InventoryEntries").HasForeignKey("ItemId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.Navigation("Character");
            entity.Navigation("Item");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.GameCharacter", entity => entity.Navigation("Inventory"));
        modelBuilder.Entity("Millionnika.Api.Entities.Item", entity => entity.Navigation("InventoryEntries"));
    }
}
