using Millionnika.Api.Tests.Infrastructure;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Admin;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;
using Millionnika.Api.Game;
using Xunit;

namespace Millionnika.Api.Tests.Admin;

[Collection("PostgreSQL integration")]
public sealed class AdminAuditIntegrationTests
{
    [Fact]
    public async Task ResourceUpdate_WritesCompleteAuditRecordInTransaction()
    {
        await using var setupDb = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var admin = CreateUser("AuditAdmin", "Admin", now);
        var player = CreateUser("AuditPlayer", "Player", now);
        setupDb.Users.AddRange(admin, player);
        await setupDb.SaveChangesAsync();

        try
        {
            await using var actionDb = CreateDbContext();
            var service = CreateService(actionDb);
            var result = await service.UpdateResourcesAsync(
                admin.Id, player.Id, new UpdateResourcesRequest(777, 70, 7, 80, "Проверка аудита"), default);

            Assert.True(result.Succeeded);
            await using var verifyDb = CreateDbContext();
            var character = await verifyDb.Characters.SingleAsync(value => value.UserId == player.Id);
            var audit = await verifyDb.AdminAuditLogs.Include(value => value.AdminUser).SingleAsync(
                value => value.AdminUserId == admin.Id && value.TargetId == player.Id.ToString());
            Assert.Equal(777, character.Credits);
            Assert.Equal("AuditAdmin", audit.AdminUser.Username);
            Assert.Equal("resources_update", audit.Action);
            Assert.Equal("Проверка аудита", audit.Reason);
            using var oldValues = JsonDocument.Parse(audit.OldValue);
            using var newValues = JsonDocument.Parse(audit.NewValue);
            Assert.Equal(100, oldValues.RootElement.GetProperty("Credits").GetInt64());
            Assert.Equal(777, newValues.RootElement.GetProperty("Credits").GetInt64());
            Assert.NotEqual(
                oldValues.RootElement.GetProperty("LastEnergyRecoveryAtUtc").GetDateTimeOffset(),
                newValues.RootElement.GetProperty("LastEnergyRecoveryAtUtc").GetDateTimeOffset());
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();
            await cleanupDb.AdminAuditLogs.Where(log => log.AdminUserId == admin.Id).ExecuteDeleteAsync();
            await cleanupDb.Users.Where(user => user.Id == admin.Id || user.Id == player.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task SavingUnchangedResources_PreservesRecoveryMarksAndAuditsThem()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-7);
        var admin = CreateUser($"Admin{Guid.NewGuid():N}"[..20], "Admin", now);
        var player = CreateUser($"Player{Guid.NewGuid():N}"[..20], "Player", now);
        await using (var setup = CreateDbContext()) { setup.Users.AddRange(admin, player); await setup.SaveChangesAsync(); }
        try
        {
            await using var db = CreateDbContext();
            var result = await CreateService(db).UpdateResourcesAsync(admin.Id, player.Id,
                new UpdateResourcesRequest(100, 100, 10, 100, "Проверка отметок"), default);
            Assert.True(result.Succeeded);
            await using var verify = CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.UserId == player.Id);
            Assert.Equal(now.ToUnixTimeMilliseconds(), character.LastEnergyRecoveryAtUtc.ToUnixTimeMilliseconds());
            Assert.Equal(now.ToUnixTimeMilliseconds(), character.LastNerveRecoveryAtUtc.ToUnixTimeMilliseconds());
            var audit = await verify.AdminAuditLogs.SingleAsync(value => value.AdminUserId == admin.Id);
            Assert.Contains("LastEnergyRecoveryAtUtc", audit.OldValue);
            Assert.Contains("LastNerveRecoveryAtUtc", audit.NewValue);
        }
        finally
        {
            await using var cleanup = CreateDbContext();
            await cleanup.AdminAuditLogs.Where(value => value.AdminUserId == admin.Id).ExecuteDeleteAsync();
            await cleanup.Users.Where(value => value.Id == admin.Id || value.Id == player.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public void LocalAdminAudit_ContainsNoAccountSecrets()
    {
        var audit = AdminAuditFactory.AdminCreated(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var serialized = audit.OldValue + audit.NewValue;
        Assert.Equal("admin_created", audit.Action);
        Assert.DoesNotContain("Password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hash", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Email", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminCanCreateContract_AndAuditContainsTexts()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = CreateUser($"Admin{Guid.NewGuid():N}"[..20], "Admin", now);
        await using (var setup = CreateDbContext()) { setup.Users.Add(admin); await setup.SaveChangesAsync(); }
        try
        {
            await using var db = CreateDbContext();
            var service = CreateService(db);
            var result = await service.CreateContractAsync(admin.Id,
                new CreateContractRequest("Тестовый контракт", "Юмор из тестовой базы.", 2, .5m, 10, 3, true,
                    "Тест создания", ContractDirections.Hacking, 1, 4, 7), default);
            Assert.True(result.Succeeded);
            await using var verify = CreateDbContext();
            var contract = await verify.Contracts.SingleAsync(value => value.Name == "Тестовый контракт");
            var audit = await verify.AdminAuditLogs.SingleAsync(value => value.TargetId == contract.Id.ToString());
            Assert.Equal("contract_create", audit.Action);
            Assert.Equal(ContractDirections.Hacking, contract.Direction);
            Assert.Equal(4, contract.MinimumHackingLevel);
            Assert.Equal(7, contract.RewardHackingExperience);
            Assert.Contains("Юмор из тестовой базы", audit.NewValue);
            Assert.Contains("RewardHackingExperience", audit.NewValue);
            Assert.DoesNotContain("Password", audit.NewValue, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await using var cleanup = CreateDbContext();
            var contractIds = await cleanup.Contracts.Where(value => value.Name == "Тестовый контракт").Select(value => value.Id).ToArrayAsync();
            await cleanup.AdminAuditLogs.Where(value => value.AdminUserId == admin.Id || contractIds.Select(id => id.ToString()).Contains(value.TargetId)).ExecuteDeleteAsync();
            await cleanup.Contracts.Where(value => contractIds.Contains(value.Id)).ExecuteDeleteAsync();
            await cleanup.Users.Where(value => value.Id == admin.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task AdminCanCreateEquipment_AndAuditContainsDefinitionWithoutSecrets()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = CreateUser($"Admin{Guid.NewGuid():N}"[..20], "Admin", now);
        await using (var setup = CreateDbContext()) { setup.Users.Add(admin); await setup.SaveChangesAsync(); }
        Guid itemId = Guid.Empty;
        try
        {
            await using var db = CreateDbContext();
            var result = await CreateService(db).CreateItemAsync(admin.Id,
                new CreateItemRequest("Тестовый клинок", "Только для проверки определения.", 25,
                    "weapon", "", 0, "melee", 10, 12, 40, 45, 0, 0, "{}", true,
                    "Проверка создания экипировки"), default);
            Assert.True(result.Succeeded);

            await using var verify = CreateDbContext();
            var item = await verify.Items.SingleAsync(value => value.Name == "Тестовый клинок");
            itemId = item.Id;
            var audit = await verify.AdminAuditLogs.SingleAsync(value => value.TargetId == item.Id.ToString());
            Assert.Equal("item_create", audit.Action);
            Assert.Equal("weapon", item.Type);
            Assert.Equal("melee", item.AllowedSlots);
            Assert.Contains("MinimumDamage", audit.NewValue);
            Assert.DoesNotContain("Password", audit.NewValue, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await using var cleanup = CreateDbContext();
            if (itemId != Guid.Empty)
            {
                await cleanup.AdminAuditLogs.Where(value => value.TargetId == itemId.ToString()).ExecuteDeleteAsync();
                await cleanup.Items.Where(value => value.Id == itemId).ExecuteDeleteAsync();
            }
            await cleanup.Users.Where(value => value.Id == admin.Id).ExecuteDeleteAsync();
        }
    }

    private static AdminService CreateService(GameDbContext db)
    {
        var options = Options.Create(new ResourceRecoveryOptions
        {
            EnergyAmount = 5, EnergyIntervalMinutes = 15,
            NerveAmount = 1, NerveIntervalMinutes = 5
        });
        return new AdminService(db, new ResourceRecoveryService(options, TimeProvider.System));
    }

    private static User CreateUser(string username, string role, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        return new User
        {
            Id = id, Username = username, NormalizedUsername = username.ToUpperInvariant(),
            Email = $"{id:N}@audit.invalid", NormalizedEmail = $"{id:N}@AUDIT.INVALID",
            PasswordHash = "integration-test-not-a-real-password", Role = role, CreatedAtUtc = now,
            Character = new GameCharacter
            {
                Id = Guid.NewGuid(), UserId = id, Level = 1, Credits = 100,
                Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now,
                Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = now,
                Health = 100, MaxHealth = 100, Strength = 1, Reflexes = 1, Toughness = 1, Agility = 1
            }
        };
    }

    private static GameDbContext CreateDbContext() => TestDatabase.CreateDbContext();

}
