using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Hunting;
using Millionnika.Api.Inventory;
using Millionnika.Api.Resources;
using Millionnika.Api.Tests.Infrastructure;
using Xunit;

namespace Millionnika.Api.Tests.Hunting;

[Collection("PostgreSQL integration")]
public sealed class HuntingServiceIntegrationTests
{
    private static readonly Guid TargetId = Guid.Parse("55555555-5555-5555-5555-555555555001");
    private static readonly Guid MeleeId = Guid.Parse("22222222-2222-2222-2222-222222222003");

    [Fact]
    public async Task StartAndTurn_AreIdempotentAndParallelTurnCannotSkipCooldown()
    {
        var user = CreateUser();
        await using (var setup = TestDatabase.CreateDbContext())
        {
            setup.Users.Add(user);
            setup.EquipmentInstances.Add(new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character!.Id,
                ItemId = MeleeId, Damage = 16, Accuracy = 100, EquippedSlot = EquipmentSlots.Melee, CreatedAtUtc = Clock.GetUtcNow() });
            await setup.SaveChangesAsync();
        }
        try
        {
            var startId = Guid.NewGuid();
            await using (var db = TestDatabase.CreateDbContext())
            {
                var result = await Service(db).StartAsync(user.Id, new(startId, TargetId), default);
                Assert.Equal(200, result.StatusCode); Assert.Equal(90, result.Response.Energy);
            }
            await using (var db = TestDatabase.CreateDbContext())
            {
                var replay = await Service(db).StartAsync(user.Id, new(startId, TargetId), default);
                Assert.True(replay.Response.Replayed);
                var conflict = await Service(db).StartAsync(user.Id, new(startId, Guid.NewGuid()), default);
                Assert.Equal(409, conflict.StatusCode);
            }

            var turnId = Guid.NewGuid();
            await using (var db = TestDatabase.CreateDbContext())
            {
                var turn = await Service(db).TurnAsync(user.Id, new(turnId, "attack", EquipmentSlots.Melee), default);
                Assert.Equal(200, turn.StatusCode); Assert.Equal(1, turn.Response.Turn);
            }
            await using (var db = TestDatabase.CreateDbContext())
            {
                var replay = await Service(db).TurnAsync(user.Id, new(turnId, "attack", EquipmentSlots.Melee), default);
                Assert.True(replay.Response.Replayed); Assert.Equal(1, replay.Response.Turn);
            }
            await using var first = TestDatabase.CreateDbContext(); await using var second = TestDatabase.CreateDbContext();
            var results = await Task.WhenAll(
                Service(first).TurnAsync(user.Id, new(Guid.NewGuid(), "attack", EquipmentSlots.Melee), default),
                Service(second).TurnAsync(user.Id, new(Guid.NewGuid(), "attack", EquipmentSlots.Melee), default));
            Assert.All(results, result => Assert.Equal(429, result.StatusCode));
            await using var verify = TestDatabase.CreateDbContext();
            Assert.Equal(1, await verify.HuntFights.Where(value => value.CharacterId == user.Character!.Id).Select(value => value.Turn).SingleAsync());
            Assert.Equal(90, await verify.Characters.Where(value => value.Id == user.Character!.Id).Select(value => value.Energy).SingleAsync());
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.Users.Where(value => value.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task DefeatRecoveryAndResultReplay_AreAppliedOnlyOnce()
    {
        Clock.Set(DateTimeOffset.Parse("2026-10-11T01:00:00Z"));
        var user = CreateUser(); user.Character!.Health = 1; user.Character.MaxHealth = 100;
        await using (var setup = TestDatabase.CreateDbContext())
        {
            setup.Users.Add(user);
            setup.EquipmentInstances.Add(new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character.Id,
                ItemId = MeleeId, Damage = 1, Accuracy = 100, EquippedSlot = EquipmentSlots.Melee, CreatedAtUtc = Clock.GetUtcNow() });
            await setup.SaveChangesAsync();
        }
        try
        {
            await using (var db = TestDatabase.CreateDbContext())
                Assert.Equal(200, (await Service(db).StartAsync(user.Id, new(Guid.NewGuid(), TargetId), default)).StatusCode);
            var turnId = Guid.NewGuid();
            await using (var db = TestDatabase.CreateDbContext())
            {
                var result = await Service(db).TurnAsync(user.Id, new(turnId, "attack", EquipmentSlots.Melee), default);
                Assert.Equal("lost", result.Response.Status); Assert.Equal(25, result.Response.PlayerHealth);
            }
            await using (var db = TestDatabase.CreateDbContext())
            {
                var replay = await Service(db).TurnAsync(user.Id, new(turnId, "attack", EquipmentSlots.Melee), default);
                Assert.True(replay.Response.Replayed); Assert.Equal(25, replay.Response.PlayerHealth);
                var read = await Service(db).GetCurrentFightAsync(user.Id, default);
                Assert.NotNull(read); Assert.Equal(25, read!.PlayerHealth);
            }
            await using var verify = TestDatabase.CreateDbContext();
            Assert.Equal(25, await verify.Characters.Where(value => value.Id == user.Character.Id).Select(value => value.Health).SingleAsync());
            Assert.Equal(0, await verify.Characters.Where(value => value.Id == user.Character.Id).Select(value => value.HuntingExperience).SingleAsync());
        }
        finally { await using var cleanup = TestDatabase.CreateDbContext(); await cleanup.Users.Where(value => value.Id == user.Id).ExecuteDeleteAsync(); }
    }

    private static readonly MutableTimeProvider Clock = new(DateTimeOffset.Parse("2026-10-11T00:00:00Z"));
    private static HuntingService Service(GameDbContext db)
    {
        var options = Options.Create(Settings());
        return new(db, new CombatCalculator(options, new ZeroRandom()), TestProgression.Create(),
            new ResourceRecoveryService(Options.Create(new ResourceRecoveryOptions { EnergyAmount = 5, EnergyIntervalMinutes = 15, NerveAmount = 1, NerveIntervalMinutes = 5 }), Clock), options);
    }

    private static User CreateUser()
    {
        var id = Guid.NewGuid(); var now = Clock.GetUtcNow();
        return new User { Id = id, Username = $"Hunt{id:N}"[..20], NormalizedUsername = $"HUNT{id:N}"[..20], Email = $"{id:N}@hunt.invalid", NormalizedEmail = $"{id:N}@HUNT.INVALID", PasswordHash = "integration-test-not-a-real-password", CreatedAtUtc = now,
            Character = new GameCharacter { Id = Guid.NewGuid(), UserId = id, Level = 1, Credits = 100, Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now, Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = now, Health = 100, MaxHealth = 100, Strength = 1, Speed = 1, Defense = 1, Dexterity = 1, HuntingLevel = 1 } };
    }

    private static HuntingOptions Settings() => new()
    {
        MaximumLevel = 20, MaximumTurns = 25,
        FightDurationSeconds = 300, ActionCooldownSeconds = 1, MaximumSpareClips = 3, BaseDamageScale = 2,
        CreditBonusPerExtraLevel = .02m, MaximumCreditBonus = .2m, DefeatHealthFloor = .25m,
        BodyPartWeights = new() { ["head"] = 8, ["throat"] = 3, ["heart"] = 4, ["chest"] = 20, ["stomach"] = 15, ["arms"] = 15, ["hands"] = 5, ["groin"] = 5, ["legs"] = 20, ["feet"] = 5 },
        BodyPartDamageMultipliers = new() { ["head"] = 3.5m, ["throat"] = 3.5m, ["heart"] = 3.5m, ["chest"] = 2, ["stomach"] = 2, ["arms"] = 1, ["hands"] = .7m, ["groin"] = 2, ["legs"] = 1, ["feet"] = .7m },
        SpeedRatios = [.25m, .5m, 1, 2, 4], HitChances = [10, 25, 50, 75, 90], DefenseRatios = [.25m, .5m, 1, 2, 4], DamageMitigations = [10, 25, 50, 75, 90]
    };
    private sealed class ZeroRandom : IRandomSource { public double NextDouble() => 0; }
    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset current = value;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }
}
