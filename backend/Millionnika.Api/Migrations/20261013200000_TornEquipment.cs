using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261013200000_TornEquipment")]
public sealed class TornEquipment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("AllowedSlots", "items", "character varying(80)", maxLength: 80, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("ArmorCoverage", "items", "jsonb", nullable: false, defaultValue: "{}");
        migrationBuilder.AddColumn<decimal>("MaximumAccuracy", "items", "numeric(5,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("MaximumArmor", "items", "numeric(5,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("MaximumDamage", "items", "numeric(7,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("MinimumAccuracy", "items", "numeric(5,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("MinimumArmor", "items", "numeric(5,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("MinimumDamage", "items", "numeric(7,2)", nullable: false, defaultValue: 0m);

        migrationBuilder.Sql("""
            DO $equipment_check$
            BEGIN
                IF EXISTS (SELECT 1 FROM items WHERE "Type" <> 'consumable' OR "EffectAmount" <= 0) THEN
                    RAISE EXCEPTION USING ERRCODE = '23514',
                        MESSAGE = 'Existing item data is incompatible with TornEquipment. Migration stopped without changing item types.';
                END IF;
            END $equipment_check$;
            """);

        migrationBuilder.DropCheckConstraint("CK_items_EffectAmount", "items");
        migrationBuilder.AddCheckConstraint("CK_items_EffectAmount", "items", "(\"Type\" = 'consumable' AND \"EffectAmount\" > 0) OR (\"Type\" IN ('weapon','armor') AND \"EffectAmount\" = 0)");
        migrationBuilder.AddCheckConstraint("CK_items_EquipmentRatings", "items", "\"MinimumDamage\" BETWEEN 0 AND 1000 AND \"MaximumDamage\" BETWEEN \"MinimumDamage\" AND 1000 AND \"MinimumAccuracy\" BETWEEN 0 AND 100 AND \"MaximumAccuracy\" BETWEEN \"MinimumAccuracy\" AND 100 AND \"MinimumArmor\" BETWEEN 0 AND 100 AND \"MaximumArmor\" BETWEEN \"MinimumArmor\" AND 100");

        migrationBuilder.CreateTable(
            name: "equipment_instances",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                CharacterId = table.Column<Guid>("uuid", nullable: false),
                ItemId = table.Column<Guid>("uuid", nullable: false),
                Damage = table.Column<decimal>("numeric(7,2)", nullable: false),
                Accuracy = table.Column<decimal>("numeric(5,2)", nullable: false),
                Armor = table.Column<decimal>("numeric(5,2)", nullable: false),
                EquippedSlot = table.Column<string>("character varying(16)", maxLength: 16, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_equipment_instances", value => value.Id);
                table.CheckConstraint("CK_equipment_instances_Ratings", "\"Damage\" BETWEEN 0 AND 1000 AND \"Accuracy\" BETWEEN 0 AND 100 AND \"Armor\" BETWEEN 0 AND 100");
                table.CheckConstraint("CK_equipment_instances_Slot", "\"EquippedSlot\" IS NULL OR \"EquippedSlot\" IN ('primary','secondary','melee','temporary','helmet','body','pants','gloves','boots')");
                table.ForeignKey("FK_equipment_instances_characters_CharacterId", value => value.CharacterId, "characters", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_equipment_instances_items_ItemId", value => value.ItemId, "items", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_equipment_instances_CharacterId_ItemId", "equipment_instances", new[] { "CharacterId", "ItemId" });
        migrationBuilder.CreateIndex("IX_equipment_instances_CharacterId_EquippedSlot", "equipment_instances", new[] { "CharacterId", "EquippedSlot" }, unique: true, filter: "\"EquippedSlot\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_equipment_instances_ItemId", "equipment_instances", "ItemId");

        migrationBuilder.Sql("""
            INSERT INTO items ("Id","Name","Description","Price","Type","IsActive","EffectResource","EffectAmount",
                "AllowedSlots","MinimumDamage","MaximumDamage","MinimumAccuracy","MaximumAccuracy","MinimumArmor","MaximumArmor","ArmorCoverage") VALUES
            ('22222222-2222-2222-2222-222222222001','Импульсный карабин','Городская импульсная винтовка. Damage и Accuracy — рейтинги будущего боя.',300,'weapon',TRUE,'',0,'primary',56,56,52,52,0,0,'{}'),
            ('22222222-2222-2222-2222-222222222002','Разрядник «Искра»','Компактный разрядник для дополнительного слота. Не обещает меткость владельца.',100,'weapon',TRUE,'',0,'secondary',28,28,53,53,0,0,'{}'),
            ('22222222-2222-2222-2222-222222222003','Дубинка «Смена»','Аргумент ближнего боя, одобренный ночной сменой техников.',80,'weapon',TRUE,'',0,'melee',16,16,57,57,0,0,'{}'),
            ('22222222-2222-2222-2222-222222222004','Шлем городского техника','Базовый шлем комплекта техника. Armor — рейтинг, покрытие хранится отдельно.',100,'armor',TRUE,'',0,'helmet',0,0,0,0,20,20,'{"head":35.12,"throat":8.84}'),
            ('22222222-2222-2222-2222-222222222005','Жилет городского техника','Жилет, переживший больше проверок безопасности, чем его владелец.',150,'armor',TRUE,'',0,'body',0,0,0,0,20,20,'{"heart":97.27,"chest":90.22,"stomach":95.09,"arms":6.50}'),
            ('22222222-2222-2222-2222-222222222006','Брюки городского техника','Рабочие брюки с защитой от искр и корпоративных совещаний.',120,'armor',TRUE,'',0,'pants',0,0,0,0,20,20,'{"stomach":7.50,"groin":100,"legs":100,"feet":15.49}'),
            ('22222222-2222-2222-2222-222222222007','Перчатки городского техника','Изолируют руки от проводов, но не от сомнительных решений.',60,'armor',TRUE,'',0,'gloves',0,0,0,0,20,20,'{"arms":0,"hands":100}'),
            ('22222222-2222-2222-2222-222222222008','Ботинки городского техника','Тяжёлые ботинки для кабельных тоннелей и быстрых стратегических отступлений.',80,'armor',TRUE,'',0,'boots',0,0,0,0,20,20,'{"legs":8.77,"feet":100}')
            ON CONFLICT ("Id") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("equipment_instances");
        migrationBuilder.Sql("DELETE FROM items WHERE \"Id\"::text LIKE '22222222-2222-2222-2222-22222222200%';");
        migrationBuilder.DropCheckConstraint("CK_items_EquipmentRatings", "items");
        migrationBuilder.DropCheckConstraint("CK_items_EffectAmount", "items");
        migrationBuilder.DropColumn("AllowedSlots", "items"); migrationBuilder.DropColumn("ArmorCoverage", "items");
        migrationBuilder.DropColumn("MaximumAccuracy", "items"); migrationBuilder.DropColumn("MaximumArmor", "items");
        migrationBuilder.DropColumn("MaximumDamage", "items"); migrationBuilder.DropColumn("MinimumAccuracy", "items");
        migrationBuilder.DropColumn("MinimumArmor", "items"); migrationBuilder.DropColumn("MinimumDamage", "items");
        migrationBuilder.AddCheckConstraint("CK_items_EffectAmount", "items", "\"EffectAmount\" > 0");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        ProgressionAndHacking.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.Item", entity =>
        {
            entity.Property<string>("AllowedSlots").IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            entity.Property<string>("ArmorCoverage").IsRequired().HasColumnType("jsonb");
            entity.Property<decimal>("MinimumDamage").HasPrecision(7,2).HasColumnType("numeric(7,2)");
            entity.Property<decimal>("MaximumDamage").HasPrecision(7,2).HasColumnType("numeric(7,2)");
            entity.Property<decimal>("MinimumAccuracy").HasPrecision(5,2).HasColumnType("numeric(5,2)");
            entity.Property<decimal>("MaximumAccuracy").HasPrecision(5,2).HasColumnType("numeric(5,2)");
            entity.Property<decimal>("MinimumArmor").HasPrecision(5,2).HasColumnType("numeric(5,2)");
            entity.Property<decimal>("MaximumArmor").HasPrecision(5,2).HasColumnType("numeric(5,2)");
            entity.ToTable("items", table =>
            {
                table.HasCheckConstraint("CK_items_EffectAmount", "(\"Type\" = 'consumable' AND \"EffectAmount\" > 0) OR (\"Type\" IN ('weapon','armor') AND \"EffectAmount\" = 0)");
                table.HasCheckConstraint("CK_items_EquipmentRatings", "\"MinimumDamage\" BETWEEN 0 AND 1000 AND \"MaximumDamage\" BETWEEN \"MinimumDamage\" AND 1000 AND \"MinimumAccuracy\" BETWEEN 0 AND 100 AND \"MaximumAccuracy\" BETWEEN \"MinimumAccuracy\" AND 100 AND \"MinimumArmor\" BETWEEN 0 AND 100 AND \"MaximumArmor\" BETWEEN \"MinimumArmor\" AND 100");
            });
        });
        modelBuilder.Entity("Millionnika.Api.Entities.EquipmentInstance", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uuid"); entity.HasKey("Id");
            entity.Property<Guid>("CharacterId").HasColumnType("uuid"); entity.Property<Guid>("ItemId").HasColumnType("uuid");
            entity.Property<decimal>("Damage").HasPrecision(7,2).HasColumnType("numeric(7,2)");
            entity.Property<decimal>("Accuracy").HasPrecision(5,2).HasColumnType("numeric(5,2)");
            entity.Property<decimal>("Armor").HasPrecision(5,2).HasColumnType("numeric(5,2)");
            entity.Property<string>("EquippedSlot").HasMaxLength(16).HasColumnType("character varying(16)");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            entity.HasIndex("CharacterId", "EquippedSlot").IsUnique().HasFilter("\"EquippedSlot\" IS NOT NULL");
            entity.HasIndex("CharacterId", "ItemId"); entity.HasIndex("ItemId");
            entity.HasOne("Millionnika.Api.Entities.GameCharacter", "Character").WithMany("Equipment").HasForeignKey("CharacterId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            entity.HasOne("Millionnika.Api.Entities.Item", "Item").WithMany("EquipmentInstances").HasForeignKey("ItemId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.ToTable("equipment_instances", table =>
            {
                table.HasCheckConstraint("CK_equipment_instances_Ratings", "\"Damage\" BETWEEN 0 AND 1000 AND \"Accuracy\" BETWEEN 0 AND 100 AND \"Armor\" BETWEEN 0 AND 100");
                table.HasCheckConstraint("CK_equipment_instances_Slot", "\"EquippedSlot\" IS NULL OR \"EquippedSlot\" IN ('primary','secondary','melee','temporary','helmet','body','pants','gloves','boots')");
            });
        });
    }
}
