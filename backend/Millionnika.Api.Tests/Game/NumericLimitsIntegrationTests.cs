using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Inventory;
using Millionnika.Api.Resources;
using Millionnika.Api.Tests.Infrastructure;
using Npgsql;
using Xunit;

namespace Millionnika.Api.Tests.Game;

[Collection("PostgreSQL integration")]
public sealed class NumericLimitsIntegrationTests
{
    [Fact]
    public async Task ContractRewardAboveLimit_DoesNotSpendOrPartiallyAward()
    {
        var player = await CreatePlayerAsync(credits: GameNumericLimits.MaxEconomyValue, experience: 20);
        await using var setup = TestDatabase.CreateDbContext();
            var contract = await setup.Contracts.SingleAsync(value => value.Id == Guid.Parse("1c92d38c-6aca-4d33-9044-a4a92f6a6101"));
        var original = new ContractValues(contract.CostNerve, contract.SuccessChance,
            contract.RewardCredits, contract.RewardExperience, contract.IsActive);
        contract.CostNerve = 2;
        contract.SuccessChance = 1;
        contract.RewardCredits = 1;
        contract.RewardExperience = 5;
        contract.IsActive = true;
        await setup.SaveChangesAsync();

        try
        {
            await using var action = TestDatabase.CreateDbContext();
            var result = await CreateGameService(action).ExecuteContractAsync(player.UserId, Guid.NewGuid(), default);
            Assert.True(result.ResourceRejected);
            Assert.False(result.Response.Succeeded);
            Assert.Contains("предел", result.Response.Message, StringComparison.OrdinalIgnoreCase);

            await using var verify = TestDatabase.CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.Id == player.CharacterId);
            Assert.Equal(GameNumericLimits.MaxEconomyValue, character.Credits);
            Assert.Equal(20, character.Experience);
            Assert.Equal(10, character.Nerve);
            Assert.Equal(player.NerveRecoveryAtUtc, character.LastNerveRecoveryAtUtc);
            var log = await verify.ActionLogs.SingleAsync(value => value.UserId == player.UserId);
            Assert.Equal(0, log.CreditsAwarded);
            Assert.Equal(0, log.ExperienceAwarded);
            Assert.Equal(0, log.NerveSpent);
        }
        finally
        {
            await RestoreContractAndDeletePlayerAsync(contract.Id, original, player.UserId);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrainingAboveStatOrExperienceLimit_DoesNotSpendEnergy(bool statAtLimit)
    {
        var player = await CreatePlayerAsync(
            credits: 100,
            experience: statAtLimit ? 0 : GameNumericLimits.MaxEconomyValue,
            strength: statAtLimit ? GameNumericLimits.MaxStatValue : 1);
        try
        {
            await using var action = TestDatabase.CreateDbContext();
            var result = await CreateGameService(action).TrainAsync(
                player.UserId, Guid.NewGuid(), TrainingStat.Strength, default);
            Assert.True(result.ResourceRejected);
            Assert.False(result.Response.Succeeded);

            await using var verify = TestDatabase.CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.Id == player.CharacterId);
            Assert.Equal(100, character.Energy);
            Assert.Equal(player.EnergyRecoveryAtUtc, character.LastEnergyRecoveryAtUtc);
            Assert.Equal(statAtLimit ? GameNumericLimits.MaxStatValue : 1, character.Strength);
            Assert.Equal(statAtLimit ? 0 : GameNumericLimits.MaxEconomyValue, character.Experience);
        }
        finally
        {
            await DeletePlayerAsync(player.UserId);
        }
    }

    [Fact]
    public async Task PurchaseTotalAboveLimit_DoesNotSpendCreditsOrAddItem()
    {
        var player = await CreatePlayerAsync(GameNumericLimits.MaxEconomyValue, 0);
        var itemId = Guid.Parse("11111111-1111-1111-1111-111111111001");
        await using var setup = TestDatabase.CreateDbContext();
        var item = await setup.Items.SingleAsync(value => value.Id == itemId);
        var originalPrice = item.Price;
        item.Price = GameNumericLimits.MaxEconomyValue;
        await setup.SaveChangesAsync();

        try
        {
            await using var action = TestDatabase.CreateDbContext();
            var result = await CreateInventoryService(action).BuyAsync(
                player.UserId, Guid.NewGuid(), itemId, 2, default);
            Assert.True(result.Rejected);
            Assert.False(result.Response.Succeeded);

            await using var verify = TestDatabase.CreateDbContext();
            Assert.Equal(GameNumericLimits.MaxEconomyValue,
                await verify.Characters.Where(value => value.Id == player.CharacterId).Select(value => value.Credits).SingleAsync());
            Assert.False(await verify.InventoryItems.AnyAsync(value => value.CharacterId == player.CharacterId));
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.Items.Where(value => value.Id == itemId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Price, originalPrice));
            await cleanup.Users.Where(value => value.Id == player.UserId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task DatabaseConstraint_RejectsValueAboveLimitAndPreservesValue()
    {
        var player = await CreatePlayerAsync(100, 0);
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE characters SET \"Credits\" = {GameNumericLimits.MaxEconomyValue + 1} WHERE \"Id\" = {player.CharacterId}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);

            await using var verify = TestDatabase.CreateDbContext();
            Assert.Equal(100, await verify.Characters.Where(value => value.Id == player.CharacterId)
                .Select(value => value.Credits).SingleAsync());
        }
        finally
        {
            await DeletePlayerAsync(player.UserId);
        }
    }

    [Fact]
    public async Task Migration_RejectsLegacyInvalidDataBeforeAddingConstraints()
    {
        var player = await CreatePlayerAsync(100, 0);
        try
        {
            await using (var down = TestDatabase.CreateDbContext())
                await down.GetService<IMigrator>().MigrateAsync("20261011120000_SecuritySessions");
            await using (var invalid = TestDatabase.CreateDbContext())
                await invalid.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE characters SET \"Credits\" = {GameNumericLimits.MaxEconomyValue + 1} WHERE \"Id\" = {player.CharacterId}");

            await using var migrate = TestDatabase.CreateDbContext();
            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                migrate.GetService<IMigrator>().MigrateAsync("20261011140000_NumericLimits"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
        finally
        {
            await using (var repair = TestDatabase.CreateDbContext())
                await repair.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE characters SET \"Credits\" = {100L} WHERE \"Id\" = {player.CharacterId}");
            await using (var migrate = TestDatabase.CreateDbContext())
                await migrate.GetService<IMigrator>().MigrateAsync();
            await DeletePlayerAsync(player.UserId);
        }
    }

    private static async Task<TestPlayer> CreatePlayerAsync(
        long credits, long experience, int strength = 1)
    {
        var userId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var current = DateTimeOffset.UtcNow;
        var now = new DateTimeOffset(current.Ticks - current.Ticks % 10, TimeSpan.Zero);
        await using var db = TestDatabase.CreateDbContext();
        db.Users.Add(new User
        {
            Id = userId,
            Username = $"Limit_{userId:N}"[..22],
            NormalizedUsername = $"LIMIT_{userId:N}"[..22],
            Email = $"{userId:N}@limits.invalid",
            NormalizedEmail = $"{userId:N}@LIMITS.INVALID",
            PasswordHash = "integration-test-not-a-real-password",
            CreatedAtUtc = now,
            Character = new GameCharacter
            {
                Id = characterId, UserId = userId, Level = 1, Credits = credits,
                Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now,
                Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = now,
                Health = 100, MaxHealth = 100, Strength = strength, Speed = 1, Defense = 1, Dexterity = 1,
                Experience = experience
            }
        });
        await db.SaveChangesAsync();
        return new TestPlayer(userId, characterId, now, now);
    }

    private static GameActionService CreateGameService(GameDbContext db) => new(
        db,
        new FixedRandom(),
        Options.Create(new TrainingOptions { EnergyCost = 10, StatGain = 1, ExperienceGain = 2 }),
        CreateRecovery());

    private static InventoryService CreateInventoryService(GameDbContext db) => new(db, CreateRecovery());

    private static ResourceRecoveryService CreateRecovery() => new(
        Options.Create(new ResourceRecoveryOptions
        {
            EnergyAmount = 5, EnergyIntervalMinutes = 15, NerveAmount = 1, NerveIntervalMinutes = 5
        }), TimeProvider.System);

    private static async Task RestoreContractAndDeletePlayerAsync(
        Guid contractId, ContractValues original, Guid userId)
    {
        await using var db = TestDatabase.CreateDbContext();
        await db.Contracts.Where(value => value.Id == contractId).ExecuteUpdateAsync(setters => setters
            .SetProperty(value => value.CostNerve, original.CostNerve)
            .SetProperty(value => value.SuccessChance, original.SuccessChance)
            .SetProperty(value => value.RewardCredits, original.RewardCredits)
            .SetProperty(value => value.RewardExperience, original.RewardExperience)
            .SetProperty(value => value.IsActive, original.IsActive));
        await db.Users.Where(value => value.Id == userId).ExecuteDeleteAsync();
    }

    private static async Task DeletePlayerAsync(Guid userId)
    {
        await using var db = TestDatabase.CreateDbContext();
        await db.Users.Where(value => value.Id == userId).ExecuteDeleteAsync();
    }

    private sealed record TestPlayer(
        Guid UserId, Guid CharacterId, DateTimeOffset EnergyRecoveryAtUtc, DateTimeOffset NerveRecoveryAtUtc);
    private sealed record ContractValues(
        int CostNerve, decimal SuccessChance, long RewardCredits, long RewardExperience, bool IsActive);
    private sealed class FixedRandom : IRandomSource { public double NextDouble() => 0; }
}
