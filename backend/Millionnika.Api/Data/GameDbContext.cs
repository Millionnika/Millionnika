using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;

namespace Millionnika.Api.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<GameCharacter> Characters => Set<GameCharacter>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ActionLog> ActionLogs => Set<ActionLog>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<PlayerInventoryItem> InventoryItems => Set<PlayerInventoryItem>();
    public DbSet<ConsumableCooldown> ConsumableCooldowns => Set<ConsumableCooldown>();
    public DbSet<EquipmentInstance> EquipmentInstances => Set<EquipmentInstance>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<FriendRelation> FriendRelations => Set<FriendRelation>();
    public DbSet<HuntingLocation> HuntingLocations => Set<HuntingLocation>();
    public DbSet<HuntingTarget> HuntingTargets => Set<HuntingTarget>();
    public DbSet<NpcTemplate> NpcTemplates => Set<NpcTemplate>();
    public DbSet<HuntingLootRow> HuntingLootRows => Set<HuntingLootRow>();
    public DbSet<HuntFight> HuntFights => Set<HuntFight>();
    public DbSet<HuntingRequest> HuntingRequests => Set<HuntingRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(session => session.Id);
            entity.HasIndex(session => session.UserId);
            entity.HasIndex(session => session.ExpiresAtUtc);
            entity.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasMaxLength(24).IsRequired();
            entity.Property(user => user.NormalizedUsername).HasMaxLength(24).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(254).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(254).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(16).IsRequired();
            entity.Property(user => user.Description).HasMaxLength(300).IsRequired();
            entity.HasIndex(user => user.NormalizedUsername).IsUnique();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
            entity.HasOne(user => user.Character)
                .WithOne(character => character.User)
                .HasForeignKey<GameCharacter>(character => character.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameCharacter>(entity =>
        {
            entity.ToTable("characters", table =>
            {
                table.HasCheckConstraint("CK_characters_Credits", $"\"Credits\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_characters_Experience", $"\"Experience\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_characters_Progression", $"\"Level\" BETWEEN 1 AND 20 AND \"HackingLevel\" BETWEEN 1 AND 20 AND \"HackingExperience\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"HuntingLevel\" BETWEEN 1 AND 20 AND \"HuntingExperience\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_characters_Stats", $"\"Strength\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue} AND \"Reflexes\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue} AND \"Toughness\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue} AND \"Agility\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue}");
            });
            entity.HasKey(character => character.Id);
            entity.HasIndex(character => character.UserId).IsUnique();
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("contracts", table =>
            {
                table.HasCheckConstraint("CK_contracts_NumericLimits", $"\"CostNerve\" > 0 AND \"SuccessChance\" BETWEEN 0 AND 1 AND \"RewardCredits\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"RewardExperience\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"RewardHackingExperience\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_contracts_Progression", "\"Direction\" IN ('normal', 'hacking') AND \"MinimumLevel\" BETWEEN 1 AND 20 AND \"MinimumHackingLevel\" BETWEEN 1 AND 20 AND (\"Direction\" = 'hacking' OR (\"MinimumHackingLevel\" = 1 AND \"RewardHackingExperience\" = 0))");
            });
            entity.HasKey(contract => contract.Id);
            entity.Property(contract => contract.Name).HasMaxLength(120).IsRequired();
            entity.Property(contract => contract.Description).HasMaxLength(500).IsRequired();
            entity.Property(contract => contract.Direction).HasMaxLength(16).IsRequired();
            entity.Property(contract => contract.SuccessChance).HasPrecision(5, 4);
        });

        modelBuilder.Entity<ActionLog>(entity =>
        {
            entity.ToTable("action_logs", table =>
            {
                table.HasCheckConstraint("CK_action_logs_Economy", $"\"CreditsAwarded\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"ExperienceAwarded\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"HackingExperienceAwarded\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"HuntingExperienceAwarded\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"CreditsSpent\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_action_logs_StatGain", $"\"StatGain\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue}");
                table.HasCheckConstraint("CK_action_logs_RequestSignature", "\"RequestSignature\" <> ''");
            });
            entity.HasKey(log => log.Id);
            entity.Property(log => log.ActionType).HasMaxLength(32).IsRequired();
            entity.Property(log => log.RequestSignature).HasMaxLength(160).IsRequired();
            entity.Property(log => log.Message).HasMaxLength(240).IsRequired();
            entity.Property(log => log.ErrorCode).HasMaxLength(40);
            entity.Property(log => log.StatName).HasMaxLength(16);
            entity.HasIndex(log => new { log.UserId, log.RequestId }).IsUnique();
            entity.HasOne(log => log.User)
                .WithMany(user => user.ActionLogs)
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("items", table =>
            {
                table.HasCheckConstraint("CK_items_Price", $"\"Price\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_items_EffectAmount", "(\"Type\" = 'consumable' AND \"EffectAmount\" > 0) OR (\"Type\" IN ('weapon','armor','ammunition','material') AND \"EffectAmount\" = 0)");
                table.HasCheckConstraint("CK_items_EquipmentRatings", "\"MinimumDamage\" BETWEEN 0 AND 1000 AND \"MaximumDamage\" BETWEEN \"MinimumDamage\" AND 1000 AND \"MinimumAccuracy\" BETWEEN 0 AND 100 AND \"MaximumAccuracy\" BETWEEN \"MinimumAccuracy\" AND 100 AND \"MinimumArmor\" BETWEEN 0 AND 100 AND \"MaximumArmor\" BETWEEN \"MinimumArmor\" AND 100");
                table.HasCheckConstraint("CK_items_Ammo", "\"ClipSize\" BETWEEN 0 AND 1000 AND \"RateOfFireMin\" BETWEEN 0 AND 1000 AND \"RateOfFireMax\" BETWEEN \"RateOfFireMin\" AND 1000");
                table.HasCheckConstraint("CK_items_Cooldown", "\"CooldownSeconds\" BETWEEN 0 AND 86400 AND ((\"Type\" = 'consumable' AND ((\"CooldownSeconds\" = 0 AND \"CooldownGroup\" = '') OR (\"CooldownSeconds\" > 0 AND \"CooldownGroup\" <> ''))) OR (\"Type\" <> 'consumable' AND \"CooldownSeconds\" = 0 AND \"CooldownGroup\" = ''))");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Type).HasMaxLength(32).IsRequired();
            entity.Property(item => item.EffectResource).HasMaxLength(16).IsRequired();
            entity.Property(item => item.AllowedSlots).HasMaxLength(80).IsRequired();
            entity.Property(item => item.MinimumDamage).HasPrecision(7, 2);
            entity.Property(item => item.MaximumDamage).HasPrecision(7, 2);
            entity.Property(item => item.MinimumAccuracy).HasPrecision(5, 2);
            entity.Property(item => item.MaximumAccuracy).HasPrecision(5, 2);
            entity.Property(item => item.MinimumArmor).HasPrecision(5, 2);
            entity.Property(item => item.MaximumArmor).HasPrecision(5, 2);
            entity.Property(item => item.ArmorCoverage).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.Caliber).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CooldownGroup).HasMaxLength(64).IsRequired();
            entity.HasData(
                new Item
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111001"),
                    Name = "Нейро-снэк",
                    Description = "Быстрый нейропитательный батончик. Восстанавливает 10 энергии.",
                    Price = 25,
                    Type = "consumable",
                    IsActive = true,
                    EffectResource = "energy",
                    EffectAmount = 10
                },
                new Item
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111002"),
                    Name = "Успокоитель",
                    Description = "Стабилизирует нервную систему. Восстанавливает 3 хладнокровия.",
                    Price = 20,
                    Type = "consumable",
                    IsActive = true,
                    EffectResource = "nerve",
                    EffectAmount = 3
                },
                new Item
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111003"),
                    Name = "Аптечный наногель",
                    Description = "Медицинский наногель. Восстанавливает 20 здоровья.",
                    Price = 35,
                    Type = "consumable",
                    IsActive = true,
                    EffectResource = "health",
                    EffectAmount = 20
                });
        });

        modelBuilder.Entity<ConsumableCooldown>(entity =>
        {
            entity.ToTable("consumable_cooldowns");
            entity.HasKey(value => new { value.CharacterId, value.Group });
            entity.Property(value => value.Group).HasMaxLength(64).IsRequired();
            entity.HasOne<GameCharacter>().WithMany().HasForeignKey(value => value.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerInventoryItem>(entity =>
        {
            entity.ToTable("player_inventory", table =>
                table.HasCheckConstraint("CK_player_inventory_Quantity", "\"Quantity\" > 0"));
            entity.HasKey(entry => entry.Id);
            entity.HasIndex(entry => new { entry.CharacterId, entry.ItemId }).IsUnique();
            entity.HasOne(entry => entry.Character)
                .WithMany(character => character.Inventory)
                .HasForeignKey(entry => entry.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(entry => entry.Item)
                .WithMany(item => item.InventoryEntries)
                .HasForeignKey(entry => entry.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EquipmentInstance>(entity =>
        {
            entity.ToTable("equipment_instances", table =>
            {
                table.HasCheckConstraint("CK_equipment_instances_Ratings", "\"Damage\" BETWEEN 0 AND 1000 AND \"Accuracy\" BETWEEN 0 AND 100 AND \"Armor\" BETWEEN 0 AND 100");
                table.HasCheckConstraint("CK_equipment_instances_Slot", "\"EquippedSlot\" IS NULL OR \"EquippedSlot\" IN ('primary','secondary','melee','temporary','helmet','body','pants','gloves','boots')");
            });
            entity.HasKey(instance => instance.Id);
            entity.Property(instance => instance.Damage).HasPrecision(7, 2);
            entity.Property(instance => instance.Accuracy).HasPrecision(5, 2);
            entity.Property(instance => instance.Armor).HasPrecision(5, 2);
            entity.Property(instance => instance.EquippedSlot).HasMaxLength(16);
            entity.HasIndex(instance => new { instance.CharacterId, instance.EquippedSlot }).IsUnique()
                .HasFilter("\"EquippedSlot\" IS NOT NULL");
            entity.HasIndex(instance => new { instance.CharacterId, instance.ItemId });
            entity.HasOne(instance => instance.Character).WithMany(character => character.Equipment)
                .HasForeignKey(instance => instance.CharacterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(instance => instance.Item).WithMany(item => item.EquipmentInstances)
                .HasForeignKey(instance => instance.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.ToTable("admin_audit_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Action).HasMaxLength(40).IsRequired();
            entity.Property(log => log.TargetType).HasMaxLength(40).IsRequired();
            entity.Property(log => log.TargetId).HasMaxLength(64).IsRequired();
            entity.Property(log => log.OldValue).HasColumnType("jsonb").IsRequired();
            entity.Property(log => log.NewValue).HasColumnType("jsonb").IsRequired();
            entity.Property(log => log.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(log => log.CreatedAtUtc);
            entity.HasOne(log => log.AdminUser)
                .WithMany(user => user.AdminAuditLogs)
                .HasForeignKey(log => log.AdminUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FriendRelation>(entity =>
        {
            entity.ToTable("friend_relations", table =>
            {
                table.HasCheckConstraint("CK_friend_relations_DifferentUsers", "\"UserLowId\" <> \"UserHighId\"");
                table.HasCheckConstraint("CK_friend_relations_Status", "\"Status\" IN ('Pending', 'Accepted')");
            });
            entity.HasKey(relation => relation.Id);
            entity.Property(relation => relation.Status).HasMaxLength(16).IsRequired();
            entity.HasIndex(relation => new { relation.UserLowId, relation.UserHighId }).IsUnique();
            entity.HasIndex(relation => relation.SenderId);
            entity.HasIndex(relation => relation.RecipientId);
            entity.HasOne(relation => relation.UserLow).WithMany()
                .HasForeignKey(relation => relation.UserLowId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(relation => relation.UserHigh).WithMany()
                .HasForeignKey(relation => relation.UserHighId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(relation => relation.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(relation => relation.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HuntingLocation>(entity =>
        {
            entity.ToTable("hunting_locations", table => table.HasCheckConstraint("CK_hunting_locations_Level", "\"MinimumHuntingLevel\" BETWEEN 1 AND 20"));
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(120).IsRequired();
            entity.Property(value => value.Description).HasMaxLength(500).IsRequired();
        });
        modelBuilder.Entity<HuntingTarget>(entity =>
        {
            entity.ToTable("hunting_targets", table =>
            {
                table.HasCheckConstraint("CK_hunting_targets_Level", "\"MinimumHuntingLevel\" BETWEEN 1 AND 20");
                table.HasCheckConstraint("CK_hunting_targets_Values", $"\"EnergyCost\" > 0 AND \"Health\" > 0 AND \"Strength\" > 0 AND \"Reflexes\" > 0 AND \"Toughness\" > 0 AND \"Agility\" > 0 AND \"RewardCredits\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"RewardExperience\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"RewardHuntingExperience\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_hunting_targets_CreditRange", $"\"MinimumCredits\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"MaximumCredits\" BETWEEN \"MinimumCredits\" AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_hunting_targets_Weapon", "\"WeaponDamage\" BETWEEN 1 AND 1000 AND \"WeaponAccuracy\" BETWEEN 0 AND 100 AND \"ClipSize\" BETWEEN 0 AND 1000 AND \"RateOfFireMin\" BETWEEN 0 AND 1000 AND \"RateOfFireMax\" BETWEEN \"RateOfFireMin\" AND 1000 AND \"SpareClips\" BETWEEN 0 AND 3");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(120).IsRequired();
            entity.Property(value => value.Description).HasMaxLength(500).IsRequired();
            entity.Property(value => value.PortraitPath).HasMaxLength(500);
            entity.Property(value => value.FigurePath).HasMaxLength(500);
            entity.Property(value => value.WeaponName).HasMaxLength(120).IsRequired();
            entity.Property(value => value.WeaponDamage).HasPrecision(7, 2);
            entity.Property(value => value.WeaponAccuracy).HasPrecision(5, 2);
            entity.Property(value => value.Armor).HasPrecision(5, 2);
            entity.Property(value => value.ArmorCoverage).HasColumnType("jsonb").IsRequired();
            entity.HasOne(value => value.Location).WithMany(value => value.Targets).HasForeignKey(value => value.LocationId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<NpcTemplate>(entity =>
        {
            entity.ToTable("npc_templates", table =>
            {
                table.HasCheckConstraint("CK_npc_templates_Category", "\"Category\" IN ('peaceful','robot','mutant','animal')");
                table.HasCheckConstraint("CK_npc_templates_Combat", "\"Category\" = 'peaceful' OR COALESCE((\"Health\" BETWEEN 1 AND 1000000 AND \"Strength\" BETWEEN 1 AND 1000000 AND \"Toughness\" BETWEEN 1 AND 1000000 AND \"Reflexes\" BETWEEN 1 AND 1000000 AND \"Agility\" BETWEEN 1 AND 1000000 AND \"WeaponName\" IS NOT NULL AND \"WeaponName\" <> '' AND \"WeaponDamage\" BETWEEN 1 AND 1000 AND \"WeaponAccuracy\" BETWEEN 0 AND 100 AND \"Armor\" BETWEEN 0 AND 100 AND \"ArmorCoverage\" IS NOT NULL), FALSE)");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(120).IsRequired();
            entity.Property(value => value.Description).HasMaxLength(500).IsRequired();
            entity.Property(value => value.Category).HasMaxLength(16).IsRequired();
            entity.Property(value => value.PortraitPath).HasMaxLength(500);
            entity.Property(value => value.FigurePath).HasMaxLength(500);
            entity.Property(value => value.WeaponName).HasMaxLength(120);
            entity.Property(value => value.WeaponDamage).HasPrecision(7, 2);
            entity.Property(value => value.WeaponAccuracy).HasPrecision(5, 2);
            entity.Property(value => value.Armor).HasPrecision(5, 2);
            entity.Property(value => value.ArmorCoverage).HasColumnType("jsonb");
        });
        modelBuilder.Entity<HuntingLootRow>(entity =>
        {
            entity.ToTable("hunting_loot_rows", table => table.HasCheckConstraint("CK_hunting_loot_rows_Values",
                "\"ChancePercent\" BETWEEN 0 AND 100 AND \"MinimumQuantity\" BETWEEN 1 AND 100 AND \"MaximumQuantity\" BETWEEN \"MinimumQuantity\" AND 100"));
            entity.HasKey(value => value.Id);
            entity.Property(value => value.ChancePercent).HasPrecision(6, 3);
            entity.HasIndex(value => new { value.TargetId, value.ItemId }).IsUnique();
            entity.HasOne(value => value.Target).WithMany(value => value.LootRows).HasForeignKey(value => value.TargetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.Item).WithMany().HasForeignKey(value => value.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HuntFight>(entity =>
        {
            entity.ToTable("hunt_fights", table =>
            {
                table.HasCheckConstraint("CK_hunt_fights_Status", "\"Status\" IN ('active','won','lost','stalemate','timeout')");
                table.HasCheckConstraint("CK_hunt_fights_Values", "\"Turn\" BETWEEN 0 AND 25 AND \"PlayerHealth\" >= 0 AND \"RobotHealth\" >= 0");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Status).HasMaxLength(16).IsRequired();
            entity.Property(value => value.SnapshotJson).HasColumnType("jsonb").IsRequired();
            entity.Property(value => value.StateJson).HasColumnType("jsonb").IsRequired();
            entity.Property(value => value.LogJson).HasColumnType("jsonb").IsRequired();
            entity.Property(value => value.LootJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(value => value.CharacterId).IsUnique().HasFilter("\"Status\" = 'active'");
            entity.HasOne(value => value.Character).WithMany(value => value.HuntFights).HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<HuntingTarget>().WithMany().HasForeignKey(value => value.TargetId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HuntingRequest>(entity =>
        {
            entity.ToTable("hunting_requests");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.ActionType).HasMaxLength(24).IsRequired();
            entity.Property(value => value.Signature).HasMaxLength(160).IsRequired();
            entity.Property(value => value.ResponseJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(value => new { value.UserId, value.RequestId }).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
