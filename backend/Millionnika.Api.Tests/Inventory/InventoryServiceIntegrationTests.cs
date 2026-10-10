using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Inventory;
using Millionnika.Api.Resources;
using Xunit;

namespace Millionnika.Api.Tests.Inventory;

public sealed class InventoryServiceIntegrationTests
{
    private static readonly Guid EnergyItemId = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly Guid HealthItemId = Guid.Parse("11111111-1111-1111-1111-111111111003");
    private static readonly Lazy<Task> Migration = new(ApplyMigrationsAsync);

    [Fact]
    public async Task Buy_AddsItemAndSubtractsCredits()
    {
        await WithPlayerAsync(100, async player =>
        {
            await using var db = CreateDbContext();
            var result = await CreateService(db).BuyAsync(player.UserId, Guid.NewGuid(), EnergyItemId, 2, default);

            Assert.True(result.Response.Succeeded);
            Assert.Equal(50, result.Response.Credits);
            Assert.Equal(2, result.Response.InventoryQuantity);
        });
    }

    [Fact]
    public async Task Buy_RejectsInsufficientCredits()
    {
        await WithPlayerAsync(10, async player =>
        {
            await using var db = CreateDbContext();
            var result = await CreateService(db).BuyAsync(player.UserId, Guid.NewGuid(), EnergyItemId, 1, default);

            Assert.True(result.Rejected);
            Assert.Equal(10, result.Response.Credits);
            Assert.Equal(0, result.Response.InventoryQuantity);
        });
    }

    [Fact]
    public async Task UseItem_SubtractsItemAndRestoresResource()
    {
        await WithPlayerAsync(100, async player =>
        {
            await AddInventoryAsync(player.CharacterId, EnergyItemId, 1, energy: 50);
            await using var db = CreateDbContext();
            var result = await CreateService(db).UseAsync(player.UserId, Guid.NewGuid(), EnergyItemId, 1, default);

            Assert.True(result.Response.Succeeded);
            Assert.Equal(60, result.Response.Resources.Energy);
            Assert.Equal(0, result.Response.InventoryQuantity);
        });
    }

    [Fact]
    public async Task UseItem_RejectsMissingItem()
    {
        await WithPlayerAsync(100, async player =>
        {
            await using var db = CreateDbContext();
            var result = await CreateService(db).UseAsync(player.UserId, Guid.NewGuid(), EnergyItemId, 1, default);

            Assert.True(result.Rejected);
            Assert.Equal(100, result.Response.Resources.Energy);
        });
    }

    [Fact]
    public async Task UseItem_CapsResourceAtMaximum()
    {
        await WithPlayerAsync(100, async player =>
        {
            await AddInventoryAsync(player.CharacterId, HealthItemId, 1, health: 95);
            await using var db = CreateDbContext();
            var result = await CreateService(db).UseAsync(player.UserId, Guid.NewGuid(), HealthItemId, 1, default);

            Assert.True(result.Response.Succeeded);
            Assert.Equal(100, result.Response.Resources.Health);
        });
    }

    [Fact]
    public async Task RepeatedRequestId_BuysOnlyOnce()
    {
        await WithPlayerAsync(100, async player =>
        {
            var requestId = Guid.NewGuid();
            await using (var firstDb = CreateDbContext())
                await CreateService(firstDb).BuyAsync(player.UserId, requestId, EnergyItemId, 1, default);
            await using (var secondDb = CreateDbContext())
                await CreateService(secondDb).BuyAsync(player.UserId, requestId, EnergyItemId, 1, default);

            await using var verificationDb = CreateDbContext();
            var character = await verificationDb.Characters.SingleAsync(value => value.Id == player.CharacterId);
            var inventory = await verificationDb.InventoryItems.SingleAsync(value => value.CharacterId == player.CharacterId);
            Assert.Equal(75, character.Credits);
            Assert.Equal(1, inventory.Quantity);
        });
    }

    [Fact]
    public async Task ParallelRequests_AreSerializedWithoutLostUpdates()
    {
        await WithPlayerAsync(100, async player =>
        {
            await using var firstDb = CreateDbContext();
            await using var secondDb = CreateDbContext();
            var first = CreateService(firstDb).BuyAsync(player.UserId, Guid.NewGuid(), EnergyItemId, 1, default);
            var second = CreateService(secondDb).BuyAsync(player.UserId, Guid.NewGuid(), EnergyItemId, 1, default);
            await Task.WhenAll(first, second);

            await using var verificationDb = CreateDbContext();
            var character = await verificationDb.Characters.SingleAsync(value => value.Id == player.CharacterId);
            var inventory = await verificationDb.InventoryItems.SingleAsync(value => value.CharacterId == player.CharacterId);
            Assert.Equal(50, character.Credits);
            Assert.Equal(2, inventory.Quantity);
        });
    }

    [Fact]
    public async Task ParallelRepeatedRequestId_BuysOnlyOnce()
    {
        await WithPlayerAsync(100, async player =>
        {
            var requestId = Guid.NewGuid();
            await using var firstDb = CreateDbContext();
            await using var secondDb = CreateDbContext();
            var first = CreateService(firstDb).BuyAsync(player.UserId, requestId, EnergyItemId, 1, default);
            var second = CreateService(secondDb).BuyAsync(player.UserId, requestId, EnergyItemId, 1, default);
            await Task.WhenAll(first, second);

            await using var verificationDb = CreateDbContext();
            var character = await verificationDb.Characters.SingleAsync(value => value.Id == player.CharacterId);
            var inventory = await verificationDb.InventoryItems.SingleAsync(value => value.CharacterId == player.CharacterId);
            Assert.Equal(75, character.Credits);
            Assert.Equal(1, inventory.Quantity);
        });
    }

    [Fact]
    public async Task ParallelUseRequests_DoNotOverspendInventory()
    {
        await WithPlayerAsync(100, async player =>
        {
            await AddInventoryAsync(player.CharacterId, EnergyItemId, 2, energy: 50);
            await using var firstDb = CreateDbContext();
            await using var secondDb = CreateDbContext();
            var first = CreateService(firstDb).UseAsync(player.UserId, Guid.NewGuid(), EnergyItemId, 1, default);
            var second = CreateService(secondDb).UseAsync(player.UserId, Guid.NewGuid(), EnergyItemId, 1, default);
            await Task.WhenAll(first, second);

            await using var verificationDb = CreateDbContext();
            var character = await verificationDb.Characters.SingleAsync(value => value.Id == player.CharacterId);
            var inventoryQuantity = await verificationDb.InventoryItems
                .Where(value => value.CharacterId == player.CharacterId && value.ItemId == EnergyItemId)
                .Select(value => value.Quantity)
                .SingleOrDefaultAsync();
            Assert.Equal(70, character.Energy);
            Assert.Equal(0, inventoryQuantity);
        });
    }

    private static async Task WithPlayerAsync(long credits, Func<TestPlayer, Task> test)
    {
        await Migration.Value;
        var userId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var suffix = userId.ToString("N")[..16];
        await using (var db = CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            db.Users.Add(new User
            {
                Id = userId,
                Username = $"Test_{suffix}",
                NormalizedUsername = $"TEST_{suffix}",
                Email = $"{userId:N}@test.invalid",
                NormalizedEmail = $"{userId:N}@TEST.INVALID",
                PasswordHash = "integration-test-not-a-real-password",
                CreatedAtUtc = now,
                Character = new GameCharacter
                {
                    Id = characterId,
                    UserId = userId,
                    Level = 1,
                    Credits = credits,
                    Energy = 100,
                    MaxEnergy = 100,
                    LastEnergyRecoveryAtUtc = now,
                    Nerve = 10,
                    MaxNerve = 10,
                    LastNerveRecoveryAtUtc = now,
                    Health = 100,
                    MaxHealth = 100,
                    Strength = 1,
                    Speed = 1,
                    Defense = 1,
                    Dexterity = 1
                }
            });
            await db.SaveChangesAsync();
        }

        try
        {
            await test(new TestPlayer(userId, characterId));
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();
            await cleanupDb.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static async Task AddInventoryAsync(
        Guid characterId,
        Guid itemId,
        int quantity,
        int? energy = null,
        int? health = null)
    {
        await using var db = CreateDbContext();
        var character = await db.Characters.SingleAsync(value => value.Id == characterId);
        if (energy.HasValue) character.Energy = energy.Value;
        if (health.HasValue) character.Health = health.Value;
        db.InventoryItems.Add(new PlayerInventoryItem
        {
            Id = Guid.NewGuid(), CharacterId = characterId, ItemId = itemId, Quantity = quantity
        });
        await db.SaveChangesAsync();
    }

    private static InventoryService CreateService(GameDbContext dbContext)
    {
        var options = Options.Create(new ResourceRecoveryOptions
        {
            EnergyAmount = 5,
            EnergyIntervalMinutes = 15,
            NerveAmount = 1,
            NerveIntervalMinutes = 5
        });
        return new InventoryService(dbContext, new ResourceRecoveryService(options, TimeProvider.System));
    }

    private static async Task ApplyMigrationsAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    private static GameDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;
        return new GameDbContext(options);
    }

    private static string GetConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__GameDatabase");
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;

        var secretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets", "millionnika-api-development", "secrets.json");
        using var document = JsonDocument.Parse(File.ReadAllText(secretsPath));
        return document.RootElement.GetProperty("ConnectionStrings:GameDatabase").GetString()
            ?? throw new InvalidOperationException("Строка подключения не настроена.");
    }

    private sealed record TestPlayer(Guid UserId, Guid CharacterId);
}
