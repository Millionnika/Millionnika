using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
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
    private static readonly Guid PrimaryId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid MeleeId = Guid.Parse("22222222-2222-2222-2222-222222222003");
    private static readonly Guid AmmunitionId = Guid.Parse("33333333-3333-3333-3333-333333333001");

    [Fact]
    public async Task Start_DistinguishesMissingWeaponFromMissingCompatibleAmmunition()
    {
        var noWeapon = CreateUser();
        var noAmmo = CreateUser();
        await using (var setup = TestDatabase.CreateDbContext())
        {
            setup.Users.AddRange(noWeapon, noAmmo);
            setup.EquipmentInstances.Add(new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = noAmmo.Character!.Id,
                ItemId = PrimaryId, Damage = 56, Accuracy = 52, EquippedSlot = EquipmentSlots.Primary, CreatedAtUtc = Clock.GetUtcNow() });
            await setup.SaveChangesAsync();
        }
        try
        {
            await using (var db = TestDatabase.CreateDbContext())
            {
                var result = await Service(db).StartAsync(noWeapon.Id, new(Guid.NewGuid(), TargetId), default);
                Assert.Equal(409, result.StatusCode);
                Assert.Equal("weapon_not_equipped", result.Response.ErrorCode);
                Assert.Contains("экипируйте оружие", result.Response.Message, StringComparison.OrdinalIgnoreCase);
            }
            await using (var db = TestDatabase.CreateDbContext())
            {
                var result = await Service(db).StartAsync(noAmmo.Id, new(Guid.NewGuid(), TargetId), default);
                Assert.Equal(409, result.StatusCode);
                Assert.Equal("compatible_ammo_missing", result.Response.ErrorCode);
                Assert.Equal("Импульсный карабин", result.Response.RequiredAmmoWeapon);
                Assert.Equal("Стандартный импульсный заряд", result.Response.RequiredAmmoName);
            }
            await using var verify = TestDatabase.CreateDbContext();
            Assert.Equal(100, await verify.Characters.Where(value => value.Id == noWeapon.Character!.Id).Select(value => value.Energy).SingleAsync());
            Assert.Equal(100, await verify.Characters.Where(value => value.Id == noAmmo.Character!.Id).Select(value => value.Energy).SingleAsync());
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.Users.Where(value => value.Id == noWeapon.Id || value.Id == noAmmo.Id).ExecuteDeleteAsync();
        }
    }

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
            await using (var restartedContext = TestDatabase.CreateDbContext())
            {
                var resumed = await Service(restartedContext).GetCurrentFightAsync(user.Id, default);
                Assert.NotNull(resumed);
                Assert.Equal("active", resumed!.Status);
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

    [Fact]
    public async Task FirearmAmmo_ReloadWeaponSwitchAndRestoredFightRemainConsistent()
    {
        Clock.Set(DateTimeOffset.Parse("2026-10-11T02:00:00Z"));
        var user = CreateUser();
        await using (var setup = TestDatabase.CreateDbContext())
        {
            setup.Users.Add(user);
            setup.EquipmentInstances.AddRange(
                new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character!.Id, ItemId = PrimaryId,
                    Damage = 56, Accuracy = 52, EquippedSlot = EquipmentSlots.Primary, CreatedAtUtc = Clock.GetUtcNow() },
                new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character.Id, ItemId = MeleeId,
                    Damage = 16, Accuracy = 50, EquippedSlot = EquipmentSlots.Melee, CreatedAtUtc = Clock.GetUtcNow() });
            setup.InventoryItems.Add(new PlayerInventoryItem { Id = Guid.NewGuid(), CharacterId = user.Character.Id,
                ItemId = AmmunitionId, Quantity = 35 });
            await setup.SaveChangesAsync();
        }
        try
        {
            HuntFightResponse started;
            await using (var db = TestDatabase.CreateDbContext())
                started = (await Service(db, new PredictableCombat()).StartAsync(user.Id,
                    new(Guid.NewGuid(), TargetId), default)).Response;
            var primary = Assert.Single(started.Weapons, value => value.Slot == EquipmentSlots.Primary);
            Assert.Equal((30, 5), (primary.AmmoInClip, primary.ReserveAmmo));

            await using (var db = TestDatabase.CreateDbContext())
            {
                var full = await Service(db, new PredictableCombat()).TurnAsync(user.Id,
                    new(Guid.NewGuid(), "reload", EquipmentSlots.Primary), default);
                Assert.Equal(409, full.StatusCode);
                Assert.Contains("уже полный", full.Response.Message);
            }

            await using (var db = TestDatabase.CreateDbContext())
            {
                var hit = await Service(db, new PredictableCombat()).TurnAsync(user.Id,
                    new(Guid.NewGuid(), "attack", EquipmentSlots.Primary), default);
                primary = Assert.Single(hit.Response.Weapons, value => value.Slot == EquipmentSlots.Primary);
                Assert.Equal((26, 5), (primary.AmmoInClip, primary.ReserveAmmo));
                Assert.Contains(hit.Response.Log, value => value.Contains("выпустил 4 заряда: попадание"));
            }

            Clock.Set(Clock.GetUtcNow().AddSeconds(2));
            await using (var restarted = TestDatabase.CreateDbContext())
            {
                var restored = await Service(restarted, new PredictableCombat()).GetCurrentFightAsync(user.Id, default);
                Assert.NotNull(restored);
                primary = Assert.Single(restored.Weapons, value => value.Slot == EquipmentSlots.Primary);
                Assert.Equal((26, 5), (primary.AmmoInClip, primary.ReserveAmmo));
            }

            await using (var db = TestDatabase.CreateDbContext())
            {
                var melee = await Service(db, new PredictableCombat()).TurnAsync(user.Id,
                    new(Guid.NewGuid(), "attack", EquipmentSlots.Melee), default);
                primary = Assert.Single(melee.Response.Weapons, value => value.Slot == EquipmentSlots.Primary);
                Assert.Equal((26, 5), (primary.AmmoInClip, primary.ReserveAmmo));
            }

            Clock.Set(Clock.GetUtcNow().AddSeconds(2));
            await using (var db = TestDatabase.CreateDbContext())
            {
                var miss = await Service(db, new PredictableCombat()).TurnAsync(user.Id,
                    new(Guid.NewGuid(), "attack", EquipmentSlots.Primary), default);
                primary = Assert.Single(miss.Response.Weapons, value => value.Slot == EquipmentSlots.Primary);
                Assert.Equal((22, 5), (primary.AmmoInClip, primary.ReserveAmmo));
                Assert.Contains(miss.Response.Log, value => value.Contains("выпустил 4 заряда: промах"));
            }

            await using (var db = TestDatabase.CreateDbContext())
            {
                var fight = await db.HuntFights.SingleAsync(value => value.CharacterId == user.Character!.Id && value.Status == "active");
                var state = JsonSerializer.Deserialize<HuntState>(fight.StateJson, CombatJson.Options)!;
                var index = state.Weapons.FindIndex(value => value.Slot == EquipmentSlots.Primary);
                state.Weapons[index] = state.Weapons[index] with { AmmoInClip = 2, ReserveAmmo = 5 };
                fight.StateJson = JsonSerializer.Serialize(state, CombatJson.Options);
                await db.SaveChangesAsync();
            }

            Clock.Set(Clock.GetUtcNow().AddSeconds(2));
            await using (var db = TestDatabase.CreateDbContext())
            {
                var remainder = await Service(db, new PredictableCombat()).TurnAsync(user.Id,
                    new(Guid.NewGuid(), "attack", EquipmentSlots.Primary), default);
                primary = Assert.Single(remainder.Response.Weapons, value => value.Slot == EquipmentSlots.Primary);
                Assert.Equal((0, 5), (primary.AmmoInClip, primary.ReserveAmmo));
                Assert.Contains(remainder.Response.Log, value => value.Contains("выпустил 2 заряда"));
            }

            Clock.Set(Clock.GetUtcNow().AddSeconds(2));
            await using (var db = TestDatabase.CreateDbContext())
            {
                var reload = await Service(db, new PredictableCombat()).TurnAsync(user.Id,
                    new(Guid.NewGuid(), "reload", EquipmentSlots.Primary), default);
                primary = Assert.Single(reload.Response.Weapons, value => value.Slot == EquipmentSlots.Primary);
                Assert.Equal((5, 0), (primary.AmmoInClip, primary.ReserveAmmo));
                Assert.Contains(reload.Response.Log, value => value.Contains("добавлено 5 патронов"));
            }

            Clock.Set(Clock.GetUtcNow().AddSeconds(2));
            await using (var db = TestDatabase.CreateDbContext())
            {
                var empty = await Service(db, new PredictableCombat()).TurnAsync(user.Id,
                    new(Guid.NewGuid(), "reload", EquipmentSlots.Primary), default);
                Assert.Equal(409, empty.StatusCode);
                Assert.Contains("Запас патронов пуст", empty.Response.Message);
            }
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.Users.Where(value => value.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    private static readonly MutableTimeProvider Clock = new(DateTimeOffset.Parse("2026-10-11T00:00:00Z"));
    private static HuntingService Service(GameDbContext db, ICombatCalculator? combat = null)
    {
        var options = Options.Create(Settings());
        return new(db, combat ?? new CombatCalculator(options, new ZeroRandom()), TestProgression.Create(),
            new ResourceRecoveryService(Options.Create(new ResourceRecoveryOptions { EnergyAmount = 5, EnergyIntervalMinutes = 15, NerveAmount = 1, NerveIntervalMinutes = 5 }), Clock), options);
    }

    private static User CreateUser()
    {
        var id = Guid.NewGuid(); var now = Clock.GetUtcNow();
        return new User { Id = id, Username = $"Hunt{id:N}"[..20], NormalizedUsername = $"HUNT{id:N}"[..20], Email = $"{id:N}@hunt.invalid", NormalizedEmail = $"{id:N}@HUNT.INVALID", PasswordHash = "integration-test-not-a-real-password", CreatedAtUtc = now,
            Character = new GameCharacter { Id = Guid.NewGuid(), UserId = id, Level = 1, Credits = 100, Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now, Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = now, Health = 100, MaxHealth = 100, Strength = 1, Reflexes = 1, Toughness = 1, Agility = 1, HuntingLevel = 1 } };
    }

    private static HuntingOptions Settings() => new()
    {
        MaximumLevel = 20, MaximumTurns = 25,
        FightDurationSeconds = 300, ActionCooldownSeconds = 1, MaximumSpareClips = 3, BaseDamageScale = 2,
        CreditBonusPerExtraLevel = .02m, MaximumCreditBonus = .2m, DefeatHealthFloor = .25m,
        BodyPartWeights = new() { ["head"] = 8, ["throat"] = 3, ["heart"] = 4, ["chest"] = 20, ["stomach"] = 15, ["arms"] = 15, ["hands"] = 5, ["groin"] = 5, ["legs"] = 20, ["feet"] = 5 },
        BodyPartDamageMultipliers = new() { ["head"] = 3.5m, ["throat"] = 3.5m, ["heart"] = 3.5m, ["chest"] = 2, ["stomach"] = 2, ["arms"] = 1, ["hands"] = .7m, ["groin"] = 2, ["legs"] = 1, ["feet"] = .7m },
        ReflexAgilityRatios = [.015625m, .05m, .1m, .2m, .5m, 1m, 1.5m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m, 20m, 40m, 50m, 64m],
        HitChances = [0m, 5.63m, 10.93m, 18.41m, 33.26m, 50m, 60.49m, 66.74m, 74.15m, 78.57m, 81.59m, 83.81m, 85.54m, 86.94m, 88.10m, 89.07m, 94.37m, 98.11m, 99.06m, 100m],
        ToughnessStrengthRatios = [.03125m, .0625m, .125m, .25m, .5m, 1m, 1.5m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m, 11m, 12m, 13m, 14m],
        DamageMitigations = [0m, 10m, 20m, 30m, 40m, 50m, 57.68m, 63.14m, 70.81m, 76.26m, 80.49m, 83.95m, 86.87m, 89.40m, 91.63m, 93.63m, 95.43m, 97.08m, 98.60m, 100m]
    };
    private sealed class ZeroRandom : IRandomSource { public double NextDouble() => 0; }
    private sealed class PredictableCombat : ICombatCalculator
    {
        public CombatHit Attack(CombatStats attacker, CombatStats defender, CombatWeapon weapon, IReadOnlyList<CombatArmor> armor)
        {
            if (weapon.Slot != EquipmentSlots.Primary) return new(false, "", 0, 0, 0, $"{weapon.Name}: промах.");
            var bullets = Math.Min(4, weapon.AmmoInClip);
            var hit = weapon.AmmoInClip != 26;
            return new(hit, hit ? "chest" : "", hit ? 1 : 0, 0, bullets,
                hit ? $"{weapon.Name} выпустил {bullets} заряда: попадание в грудь, урон 1."
                    : $"{weapon.Name} выпустил {bullets} заряда: промах.");
        }
    }
    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset current = value;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }
}
