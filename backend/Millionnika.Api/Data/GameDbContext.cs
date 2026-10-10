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
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<FriendRelation> FriendRelations => Set<FriendRelation>();

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
                table.HasCheckConstraint("CK_characters_Progression", $"\"Level\" BETWEEN 1 AND 20 AND \"HackingLevel\" BETWEEN 1 AND 20 AND \"HackingExperience\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
                table.HasCheckConstraint("CK_characters_Stats", $"\"Strength\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue} AND \"Speed\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue} AND \"Defense\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue} AND \"Dexterity\" BETWEEN 0 AND {GameNumericLimits.MaxStatValue}");
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
                table.HasCheckConstraint("CK_action_logs_Economy", $"\"CreditsAwarded\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"ExperienceAwarded\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"HackingExperienceAwarded\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue} AND \"CreditsSpent\" BETWEEN 0 AND {GameNumericLimits.MaxEconomyValue}");
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
                table.HasCheckConstraint("CK_items_EffectAmount", "\"EffectAmount\" > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Type).HasMaxLength(32).IsRequired();
            entity.Property(item => item.EffectResource).HasMaxLength(16).IsRequired();
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
    }
}
