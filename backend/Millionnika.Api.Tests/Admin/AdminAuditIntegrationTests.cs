using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Admin;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;
using Xunit;

namespace Millionnika.Api.Tests.Admin;

public sealed class AdminAuditIntegrationTests
{
    [Fact]
    public async Task ResourceUpdate_WritesCompleteAuditRecordInTransaction()
    {
        await using var setupDb = CreateDbContext();
        await setupDb.Database.MigrateAsync();
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
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();
            await cleanupDb.AdminAuditLogs.Where(log => log.AdminUserId == admin.Id).ExecuteDeleteAsync();
            await cleanupDb.Users.Where(user => user.Id == admin.Id || user.Id == player.Id).ExecuteDeleteAsync();
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
                Health = 100, MaxHealth = 100, Strength = 1, Speed = 1, Defense = 1, Dexterity = 1
            }
        };
    }

    private static GameDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(GetConnectionString()).Options);

    private static string GetConnectionString()
    {
        var environment = Environment.GetEnvironmentVariable("ConnectionStrings__GameDatabase");
        if (!string.IsNullOrWhiteSpace(environment)) return environment;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets", "millionnika-api-development", "secrets.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("ConnectionStrings:GameDatabase").GetString()!;
    }
}
