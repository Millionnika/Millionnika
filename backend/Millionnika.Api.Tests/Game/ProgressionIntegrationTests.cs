using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Resources;
using Millionnika.Api.Tests.Infrastructure;
using Xunit;

namespace Millionnika.Api.Tests.Game;

[Collection("PostgreSQL integration")]
public sealed class ProgressionIntegrationTests
{
    private static readonly Guid Coffee = Guid.Parse("1c92d38c-6aca-4d33-9044-a4a92f6a6201");
    private static readonly Guid Terminal = Guid.Parse("1c92d38c-6aca-4d33-9044-a4a92f6a6202");

    [Fact]
    public async Task HackingCatalog_ShowsLockedTasksWithExactReason()
    {
        var player = await CreatePlayerAsync();
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var catalog = await Service(db, 0).GetContractsAsync(player.UserId, ContractDirections.Hacking, default);
            Assert.Equal(3, catalog.Count);
            Assert.True(catalog.Single(value => value.Id == Coffee).IsAvailable);
            var locked = catalog.Single(value => value.Id == Terminal);
            Assert.False(locked.IsAvailable);
            Assert.Equal("Откроется на уровне Взлома 2", locked.LockReason);
        }
        finally { await DeletePlayerAsync(player.UserId); }
    }

    [Fact]
    public async Task LockedTask_IsRejectedBeforeSpending()
    {
        var player = await CreatePlayerAsync();
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var result = await Service(db, 0).ExecuteContractAsync(player.UserId, Guid.NewGuid(), Terminal, default);
            Assert.True(result.ResourceRejected);
            Assert.Contains("Откроется на уровне Взлома 2", result.Response.Message);
            await using var verify = TestDatabase.CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.Id == player.CharacterId);
            Assert.Equal(10, character.Nerve);
            Assert.Equal(0, character.Experience);
            Assert.Equal(0, character.HackingExperience);
        }
        finally { await DeletePlayerAsync(player.UserId); }
    }

    [Fact]
    public async Task FailedHackingTask_SpendsNerveButAwardsNoExperience()
    {
        var player = await CreatePlayerAsync();
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var result = await Service(db, 0.99).ExecuteContractAsync(player.UserId, Guid.NewGuid(), Coffee, default);
            Assert.False(result.Response.Succeeded);
            Assert.Equal(0, result.Response.HackingExperienceAwarded);
            await using var verify = TestDatabase.CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.Id == player.CharacterId);
            Assert.Equal(9, character.Nerve);
            Assert.Equal(0, character.Experience);
            Assert.Equal(0, character.HackingExperience);
        }
        finally { await DeletePlayerAsync(player.UserId); }
    }

    [Fact]
    public async Task ParallelSameRequest_AwardsHackingExperienceOnce()
    {
        var player = await CreatePlayerAsync();
        var requestId = Guid.NewGuid();
        try
        {
            await using var firstDb = TestDatabase.CreateDbContext();
            await using var secondDb = TestDatabase.CreateDbContext();
            var results = await Task.WhenAll(
                Service(firstDb, 0).ExecuteContractAsync(player.UserId, requestId, Coffee, default),
                Service(secondDb, 0).ExecuteContractAsync(player.UserId, requestId, Coffee, default));
            Assert.Single(results, value => value.Response.Replayed);
            await using var verify = TestDatabase.CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.Id == player.CharacterId);
            Assert.Equal(5, character.Experience);
            Assert.Equal(10, character.HackingExperience);
            Assert.Equal(9, character.Nerve);
            Assert.Equal(1, await verify.ActionLogs.CountAsync(value => value.UserId == player.UserId));
        }
        finally { await DeletePlayerAsync(player.UserId); }
    }

    [Fact]
    public async Task HackingRewardOverflow_RollsBackEntireAction()
    {
        var player = await CreatePlayerAsync(GameNumericLimits.MaxEconomyValue);
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var result = await Service(db, 0).ExecuteContractAsync(player.UserId, Guid.NewGuid(), Coffee, default);
            Assert.True(result.ResourceRejected);
            await using var verify = TestDatabase.CreateDbContext();
            var character = await verify.Characters.SingleAsync(value => value.Id == player.CharacterId);
            Assert.Equal(10, character.Nerve);
            Assert.Equal(100, character.Credits);
            Assert.Equal(0, character.Experience);
            Assert.Equal(GameNumericLimits.MaxEconomyValue, character.HackingExperience);
        }
        finally { await DeletePlayerAsync(player.UserId); }
    }

    private static async Task<(Guid UserId, Guid CharacterId)> CreatePlayerAsync(long hackingExperience = 0)
    {
        var userId = Guid.NewGuid(); var characterId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await using var db = TestDatabase.CreateDbContext();
        db.Users.Add(new User { Id = userId, Username = $"Progress_{userId:N}"[..24], NormalizedUsername = $"PROGRESS_{userId:N}"[..24], Email = $"{userId:N}@progress.invalid", NormalizedEmail = $"{userId:N}@PROGRESS.INVALID", PasswordHash = "test-only", CreatedAtUtc = now,
            Character = new GameCharacter { Id = characterId, UserId = userId, Level = 1, Experience = 0, HackingLevel = hackingExperience == 0 ? 1 : 20, HackingExperience = hackingExperience, Credits = 100, Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now, Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = now, Health = 100, MaxHealth = 100, Strength = 1, Reflexes = 1, Toughness = 1, Agility = 1 } });
        await db.SaveChangesAsync(); return (userId, characterId);
    }

    private static GameActionService Service(GameDbContext db, double roll) => new(db, new FixedRandom(roll), Options.Create(new TrainingOptions { EnergyCost = 10, StatGain = 1, ExperienceGain = 2 }), new ResourceRecoveryService(Options.Create(new ResourceRecoveryOptions { EnergyAmount = 5, EnergyIntervalMinutes = 15, NerveAmount = 1, NerveIntervalMinutes = 5 }), TimeProvider.System), TestProgression.Create());
    private static async Task DeletePlayerAsync(Guid id) { await using var db = TestDatabase.CreateDbContext(); await db.Users.Where(value => value.Id == id).ExecuteDeleteAsync(); }
    private sealed class FixedRandom(double value) : IRandomSource { public double NextDouble() => value; }
}
