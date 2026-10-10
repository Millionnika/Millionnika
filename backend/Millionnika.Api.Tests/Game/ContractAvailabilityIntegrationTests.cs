using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Resources;
using Millionnika.Api.Tests.Infrastructure;
using Npgsql;
using Xunit;

namespace Millionnika.Api.Tests.Game;

[Collection("PostgreSQL integration")]
public sealed class ContractAvailabilityIntegrationTests
{
    [Fact]
    public async Task NoActiveContract_GetReturnsNull()
    {
        var original = await ConfigureContractAsync(isActive: false);
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            Assert.Null(await CreateService(db).GetContractAsync(default));
        }
        finally
        {
            await RestoreContractAsync(original);
        }
    }

    [Fact]
    public async Task DisabledContract_ExecutionIsRejectedWithoutSpendingOrRewards()
    {
        var player = await CreatePlayerAsync();
        var original = await ConfigureContractAsync(isActive: false);
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var result = await CreateService(db).ExecuteContractAsync(player.UserId, Guid.NewGuid(), default);
            Assert.True(result.ResourceRejected);
            Assert.False(result.Response.Succeeded);
            Assert.Contains("недоступен", result.Response.Message, StringComparison.OrdinalIgnoreCase);

            await using var verify = TestDatabase.CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.Id == player.CharacterId);
            Assert.Equal(100, character.Credits);
            Assert.Equal(0, character.Experience);
            Assert.Equal(10, character.Nerve);
            var log = await verify.ActionLogs.SingleAsync(value => value.UserId == player.UserId);
            Assert.Equal(0, log.CreditsAwarded);
            Assert.Equal(0, log.ExperienceAwarded);
            Assert.Equal(0, log.NerveSpent);
        }
        finally
        {
            await RestoreContractAsync(original);
            await DeletePlayerAsync(player.UserId);
        }
    }

    [Fact]
    public async Task NormalContract_SucceedsAndAwardsConfiguredValues()
    {
        var player = await CreatePlayerAsync();
        var original = await ConfigureContractAsync(
            isActive: true, cost: 2, chance: 1, rewardCredits: 50, rewardExperience: 10);
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var result = await CreateService(db).ExecuteContractAsync(player.UserId, Guid.NewGuid(), default);
            Assert.False(result.ResourceRejected);
            Assert.True(result.Response.Succeeded);

            await using var verify = TestDatabase.CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.Id == player.CharacterId);
            Assert.Equal(150, character.Credits);
            Assert.Equal(10, character.Experience);
            Assert.Equal(8, character.Nerve);
        }
        finally
        {
            await RestoreContractAsync(original);
            await DeletePlayerAsync(player.UserId);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Database_RejectsNonPositiveContractCost(int cost)
    {
        await using var db = TestDatabase.CreateDbContext();
        var contract = await db.Contracts.AsNoTracking().SingleAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE contracts SET \"CostNerve\" = {cost} WHERE \"Id\" = {contract.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);

        await using var verify = TestDatabase.CreateDbContext();
        Assert.True(await verify.Contracts.Where(value => value.Id == contract.Id)
            .Select(value => value.CostNerve).SingleAsync() > 0);
    }

    private static async Task<Player> CreatePlayerAsync()
    {
        var userId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var db = TestDatabase.CreateDbContext();
        db.Users.Add(new User
        {
            Id = userId, Username = $"Contract_{userId:N}"[..24],
            NormalizedUsername = $"CONTRACT_{userId:N}"[..24],
            Email = $"{userId:N}@contract.invalid", NormalizedEmail = $"{userId:N}@CONTRACT.INVALID",
            PasswordHash = "integration-test-not-a-real-password", CreatedAtUtc = now,
            Character = new GameCharacter
            {
                Id = characterId, UserId = userId, Level = 1, Credits = 100,
                Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now,
                Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = now,
                Health = 100, MaxHealth = 100, Strength = 1, Speed = 1, Defense = 1, Dexterity = 1
            }
        });
        await db.SaveChangesAsync();
        return new Player(userId, characterId);
    }

    private static async Task<ContractValues> ConfigureContractAsync(
        bool isActive, int? cost = null, decimal? chance = null,
        long? rewardCredits = null, long? rewardExperience = null)
    {
        await using var db = TestDatabase.CreateDbContext();
        var contract = await db.Contracts.SingleAsync();
        var original = new ContractValues(contract.Id, contract.CostNerve, contract.SuccessChance,
            contract.RewardCredits, contract.RewardExperience, contract.IsActive);
        contract.IsActive = isActive;
        if (cost.HasValue) contract.CostNerve = cost.Value;
        if (chance.HasValue) contract.SuccessChance = chance.Value;
        if (rewardCredits.HasValue) contract.RewardCredits = rewardCredits.Value;
        if (rewardExperience.HasValue) contract.RewardExperience = rewardExperience.Value;
        await db.SaveChangesAsync();
        return original;
    }

    private static async Task RestoreContractAsync(ContractValues value)
    {
        await using var db = TestDatabase.CreateDbContext();
        await db.Contracts.Where(contract => contract.Id == value.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(contract => contract.CostNerve, value.Cost)
            .SetProperty(contract => contract.SuccessChance, value.Chance)
            .SetProperty(contract => contract.RewardCredits, value.RewardCredits)
            .SetProperty(contract => contract.RewardExperience, value.RewardExperience)
            .SetProperty(contract => contract.IsActive, value.IsActive));
    }

    private static async Task DeletePlayerAsync(Guid userId)
    {
        await using var db = TestDatabase.CreateDbContext();
        await db.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
    }

    private static GameActionService CreateService(GameDbContext db) => new(
        db, new SuccessfulRandom(),
        Options.Create(new TrainingOptions { EnergyCost = 10, StatGain = 1, ExperienceGain = 2 }),
        new ResourceRecoveryService(Options.Create(new ResourceRecoveryOptions
        {
            EnergyAmount = 5, EnergyIntervalMinutes = 15, NerveAmount = 1, NerveIntervalMinutes = 5
        }), TimeProvider.System));

    private sealed record Player(Guid UserId, Guid CharacterId);
    private sealed record ContractValues(
        Guid Id, int Cost, decimal Chance, long RewardCredits, long RewardExperience, bool IsActive);
    private sealed class SuccessfulRandom : IRandomSource { public double NextDouble() => 0; }
}
