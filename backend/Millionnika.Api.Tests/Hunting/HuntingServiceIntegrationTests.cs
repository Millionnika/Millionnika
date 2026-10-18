using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Millionnika.Api.Configuration;
using Millionnika.Api.Admin;
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
    private static readonly Guid ArmoredTargetId = Guid.Parse("55555555-5555-5555-5555-555555555002");
    private static readonly Guid PrimaryId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid MeleeId = Guid.Parse("22222222-2222-2222-2222-222222222003");
    private static readonly Guid BodyArmorId = Guid.Parse("22222222-2222-2222-2222-222222222005");
    private static readonly Guid AmmunitionId = Guid.Parse("33333333-3333-3333-3333-333333333001");
    private static readonly Guid ScrapId = Guid.Parse("66666666-6666-6666-6666-666666666001");
    private static readonly Guid ComponentsId = Guid.Parse("66666666-6666-6666-6666-666666666002");
    private static readonly Guid SparkId = Guid.Parse("22222222-2222-2222-2222-222222222002");

    [Fact]
    public async Task RemovingLocationAssignmentDoesNotBreakAnActiveFightOrSpendEnergyAgain()
    {
        Clock.Set(DateTimeOffset.Parse("2026-10-11T03:00:00Z"));
        var user = CreateUser(); user.Role = "Admin";
        var targetId = await CreateLootTargetAsync(user, []);
        try
        {
            Guid locationId;
            await using (var db = TestDatabase.CreateDbContext())
            {
                locationId = await db.HuntingLocationTargets.Where(value => value.TargetId == targetId)
                    .Select(value => value.LocationId).SingleAsync();
                var started = await Service(db, new InstantVictoryCombat()).StartAsync(user.Id,
                    new(Guid.NewGuid(), targetId, locationId), default);
                Assert.Equal(200, started.StatusCode);
                Assert.Equal("active", started.Response.Status);
            }
            await using (var db = TestDatabase.CreateDbContext())
                Assert.True((await new HuntingAssignmentAdminService(db).RemoveAsync(user.Id, locationId,
                    targetId, new("Снимаем цель во время боя"), default)).Succeeded);
            await using (var db = TestDatabase.CreateDbContext())
            {
                var restored = await Service(db, new InstantVictoryCombat()).GetCurrentFightAsync(user.Id, default);
                Assert.Equal("active", restored!.Status);
                var completed = await Service(db, new InstantVictoryCombat()).TurnAsync(user.Id,
                    new(Guid.NewGuid(), "attack", EquipmentSlots.Melee), default);
                Assert.Equal("won", completed.Response.Status);
                var energy = completed.Response.Energy;
                var rejected = await Service(db, new InstantVictoryCombat()).StartAsync(user.Id,
                    new(Guid.NewGuid(), targetId, locationId), default);
                Assert.Equal(409, rejected.StatusCode);
                Assert.Equal(energy, rejected.Response.Energy);
            }
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.AdminAuditLogs.Where(value => value.AdminUserId == user.Id).ExecuteDeleteAsync();
            await DeleteLootFixtureAsync(user.Id, targetId);
        }
    }

    [Fact]
    public async Task AdminLootConfiguration_ValidatesPrecisionAndAuditsChanges()
    {
        var admin = CreateUser(); admin.Role = "Admin";
        var targetId = await CreateLootTargetAsync(admin, [(ScrapId, 60m, 1, 2)]);
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var target = await db.HuntingTargets.AsNoTracking().SingleAsync(value => value.Id == targetId);
            var service = new AdminService(db, new ResourceRecoveryService(Options.Create(
                new ResourceRecoveryOptions { EnergyAmount = 5, EnergyIntervalMinutes = 15,
                    NerveAmount = 1, NerveIntervalMinutes = 5 }), Clock));
            var before = Assert.Single(await service.GetHuntingTargetsAsync(default), value => value.Id == targetId);
            Assert.Equal(60m, Assert.Single(before.LootRows).ChancePercent);
            var request = new UpdateHuntingTargetRequest(target.Name, target.Description,
                target.MinimumHuntingLevel, target.EnergyCost, target.Health, target.Strength, target.Reflexes,
                target.Toughness, target.Agility, target.WeaponName, target.WeaponDamage, target.WeaponAccuracy,
                target.ClipSize, target.RateOfFireMin, target.RateOfFireMax, target.SpareClips,
                target.Armor, target.ArmorCoverage, target.RewardCredits, target.RewardExperience,
                target.RewardHuntingExperience, target.IsActive, "Тест настройки добычи", 3, 5,
                [new(SparkId, .001m, 1, 1)]);
            Assert.False((await service.UpdateHuntingTargetAsync(admin.Id, targetId,
                request with { LootRows = [new(SparkId, .0001m, 1, 1)] }, default)).Succeeded);
            Assert.True((await service.UpdateHuntingTargetAsync(admin.Id, targetId, request, default)).Succeeded);
            var after = Assert.Single(await service.GetHuntingTargetsAsync(default), value => value.Id == targetId);
            Assert.Equal((3, 5), (after.MinimumCredits, after.MaximumCredits));
            Assert.Equal(.001m, Assert.Single(after.LootRows).ChancePercent);
            var audit = await db.AdminAuditLogs.SingleAsync(value => value.AdminUserId == admin.Id && value.TargetId == targetId.ToString());
            Assert.Contains("60", audit.OldValue);
            Assert.Contains("0.001", audit.NewValue);
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.AdminAuditLogs.Where(value => value.AdminUserId == admin.Id).ExecuteDeleteAsync();
            await DeleteLootFixtureAsync(admin.Id, targetId);
        }
    }

    [Fact]
    public async Task VictoryLoot_IndependentRowsCreditsAndReplaySurviveRestart()
    {
        Clock.Set(DateTimeOffset.Parse("2026-10-11T04:00:00Z"));
        var user = CreateUser();
        var target = await CreateLootTargetAsync(user, [
            (ScrapId, 60m, 1, 2), (ComponentsId, 15m, 1, 1), (SparkId, .001m, 1, 1)]);
        try
        {
            await using (var setup = TestDatabase.CreateDbContext())
            {
                setup.InventoryItems.Add(new PlayerInventoryItem { Id = Guid.NewGuid(), CharacterId = user.Character!.Id,
                    ItemId = ScrapId, Quantity = 3 });
                await setup.SaveChangesAsync();
            }
            var startId = Guid.NewGuid();
            await using (var db = TestDatabase.CreateDbContext())
                Assert.Equal(200, (await Service(db, new InstantVictoryCombat()).StartAsync(user.Id,
                    new(startId, target), default)).StatusCode);

            var turnId = Guid.NewGuid();
            var roller = new SequenceLootRoller(3, 1, 2, 1, 1, 1, 1);
            HuntFightResponse result;
            await using (var db = TestDatabase.CreateDbContext())
                result = (await Service(db, new InstantVictoryCombat(), roller).TurnAsync(user.Id,
                    new(turnId, "attack", EquipmentSlots.Melee), default)).Response;
            Assert.Equal("won", result.Status);
            Assert.Equal(8, result.CreditsAwarded); // Existing fixed five plus three extra.
            Assert.Equal(3, result.Loot!.Count);
            Assert.Contains(result.Loot, value => value.ItemId == ScrapId && value.Quantity == 2);
            Assert.Contains(result.Loot, value => value.ItemId == ComponentsId && value.Quantity == 1);
            Assert.Contains(result.Loot, value => value.ItemId == SparkId && value.Quantity == 1);
            Assert.Contains(result.Log, value => value.Contains("Добыча: Металлолом ×2"));

            await using (var db = TestDatabase.CreateDbContext())
            {
                var replay = await Service(db, new InstantVictoryCombat(), new SequenceLootRoller()).TurnAsync(user.Id,
                    new(turnId, "attack", EquipmentSlots.Melee), default);
                Assert.True(replay.Response.Replayed);
                Assert.Equal(result.Loot, replay.Response.Loot);
                var restored = await Service(db, new InstantVictoryCombat()).GetCurrentFightAsync(user.Id, default);
                Assert.Equal(result.Loot, restored!.Loot);
                Assert.Equal(8, restored.CreditsAwarded);
            }
            await using var verify = TestDatabase.CreateDbContext();
            Assert.Equal(108, await verify.Characters.Where(value => value.Id == user.Character!.Id)
                .Select(value => value.Credits).SingleAsync());
            Assert.Equal(5, await verify.InventoryItems.Where(value => value.CharacterId == user.Character.Id && value.ItemId == ScrapId)
                .Select(value => value.Quantity).SingleAsync());
            Assert.Equal(1, await verify.InventoryItems.Where(value => value.CharacterId == user.Character.Id && value.ItemId == ComponentsId)
                .Select(value => value.Quantity).SingleAsync());
            var spark = Assert.Single(await verify.EquipmentInstances.Where(value => value.CharacterId == user.Character.Id && value.ItemId == SparkId).ToListAsync());
            Assert.True(spark.Damage >= 1);
            Assert.False(spark.EquippedSlot?.Length > 0);
        }
        finally { await DeleteLootFixtureAsync(user.Id, target); }
    }

    [Fact]
    public async Task VictoryLoot_NoDropAndParallelRequestDoNotAwardTwice()
    {
        Clock.Set(DateTimeOffset.Parse("2026-10-11T05:00:00Z"));
        var user = CreateUser();
        var target = await CreateLootTargetAsync(user, [(ScrapId, 0m, 1, 2)], 2, 2);
        try
        {
            await using (var db = TestDatabase.CreateDbContext())
                Assert.Equal(200, (await Service(db, new InstantVictoryCombat()).StartAsync(user.Id,
                    new(Guid.NewGuid(), target), default)).StatusCode);
            var turnId = Guid.NewGuid();
            await using var first = TestDatabase.CreateDbContext();
            await using var second = TestDatabase.CreateDbContext();
            var results = await Task.WhenAll(
                Service(first, new InstantVictoryCombat()).TurnAsync(user.Id, new(turnId, "attack", EquipmentSlots.Melee), default),
                Service(second, new InstantVictoryCombat()).TurnAsync(user.Id, new(turnId, "attack", EquipmentSlots.Melee), default));
            Assert.All(results, value => Assert.Equal(200, value.StatusCode));
            Assert.Single(results, value => value.Response.Replayed);
            Assert.All(results, value => Assert.Empty(value.Response.Loot!));
            await using var verify = TestDatabase.CreateDbContext();
            Assert.Equal(1, await verify.HuntFights.CountAsync(value => value.CharacterId == user.Character!.Id && value.Status == "won"));
            Assert.Equal(1, await verify.ActionLogs.CountAsync(value => value.UserId == user.Id && value.ActionType == "hunt_result"));
            Assert.Equal(0, await verify.InventoryItems.CountAsync(value => value.CharacterId == user.Character!.Id && value.ItemId == ScrapId));
            Assert.Equal(107, await verify.Characters.Where(value => value.Id == user.Character!.Id).Select(value => value.Credits).SingleAsync());
        }
        finally { await DeleteLootFixtureAsync(user.Id, target); }
    }

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
        user.Character!.HuntingLevel = 2;
        await using (var setup = TestDatabase.CreateDbContext())
        {
            setup.Users.Add(user);
            setup.EquipmentInstances.AddRange(
                new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character!.Id, ItemId = PrimaryId,
                    Damage = 56, Accuracy = 52, EquippedSlot = EquipmentSlots.Primary, CreatedAtUtc = Clock.GetUtcNow() },
                new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character.Id, ItemId = MeleeId,
                    Damage = 16, Accuracy = 50, EquippedSlot = EquipmentSlots.Melee, CreatedAtUtc = Clock.GetUtcNow() },
                new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character.Id, ItemId = BodyArmorId,
                    Armor = 20, EquippedSlot = EquipmentSlots.Body, CreatedAtUtc = Clock.GetUtcNow() });
            setup.InventoryItems.Add(new PlayerInventoryItem { Id = Guid.NewGuid(), CharacterId = user.Character.Id,
                ItemId = AmmunitionId, Quantity = 35 });
            await setup.SaveChangesAsync();
        }
        try
        {
            HuntFightResponse started;
            await using (var db = TestDatabase.CreateDbContext())
                started = (await Service(db, new PredictableCombat()).StartAsync(user.Id,
                    new(Guid.NewGuid(), ArmoredTargetId), default)).Response;
            var primary = Assert.Single(started.Weapons, value => value.Slot == EquipmentSlots.Primary);
            Assert.Equal((30, 5), (primary.AmmoInClip, primary.ReserveAmmo));
            Assert.Collection(started.Participants!,
                player => { Assert.Equal("player", player.Side); Assert.Equal(user.Username, player.Name); Assert.Equal("player", player.Type); },
                robot => { Assert.Equal("opponent", robot.Side); Assert.Equal("Сторожевой паук", robot.Name); Assert.Equal("robot", robot.Type); });
            Assert.Equal([EquipmentSlots.Primary, EquipmentSlots.Melee, EquipmentSlots.Body],
                started.PlayerEquipment!.Select(value => value.Slot));
            Assert.Single(started.RobotEquipment!);
            Assert.Equal("Импульсный шипомёт", started.RobotEquipment![0].Name);
            Assert.DoesNotContain(started.RobotEquipment, value => value.Type == ItemTypes.Armor);
            var naturalProtection = Assert.Single(started.RobotProtection!);
            Assert.Equal("natural", naturalProtection.Source);
            Assert.Equal(8, naturalProtection.Armor);
            var initialEquipment = started.PlayerEquipment!.Select(value => (value.Slot, value.Type, value.Name)).ToArray();

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
                Assert.Equal(hit.Response.RobotHealth,
                    Assert.Single(hit.Response.Participants!, value => value.Side == "opponent").Health);
                Assert.Equal(initialEquipment, hit.Response.PlayerEquipment!.Select(value => (value.Slot, value.Type, value.Name)));
            }

            Clock.Set(Clock.GetUtcNow().AddSeconds(2));
            await using (var restarted = TestDatabase.CreateDbContext())
            {
                var restored = await Service(restarted, new PredictableCombat()).GetCurrentFightAsync(user.Id, default);
                Assert.NotNull(restored);
                primary = Assert.Single(restored.Weapons, value => value.Slot == EquipmentSlots.Primary);
                Assert.Equal((26, 5), (primary.AmmoInClip, primary.ReserveAmmo));
                Assert.Equal(initialEquipment, restored.PlayerEquipment!.Select(value => (value.Slot, value.Type, value.Name)));
                Assert.Equal("Сторожевой паук", Assert.Single(restored.Participants!, value => value.Type == "robot").Name);
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

    [Fact]
    public async Task ReadAtExpirationBoundary_FinalizesInsteadOfReturningActiveAtZero()
    {
        Clock.Set(DateTimeOffset.Parse("2026-10-11T03:00:00Z"));
        var user = CreateUser();
        await using (var setup = TestDatabase.CreateDbContext())
        {
            setup.Users.Add(user);
            setup.EquipmentInstances.Add(new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character!.Id,
                ItemId = MeleeId, Damage = 16, Accuracy = 100, EquippedSlot = EquipmentSlots.Melee,
                CreatedAtUtc = Clock.GetUtcNow() });
            await setup.SaveChangesAsync();
        }
        try
        {
            HuntFightResponse started;
            await using (var db = TestDatabase.CreateDbContext())
                started = (await Service(db).StartAsync(user.Id, new(Guid.NewGuid(), TargetId), default)).Response;

            Clock.Set(started.ExpiresAtUtc);
            await using var restarted = TestDatabase.CreateDbContext();
            var expired = await Service(restarted).GetCurrentFightAsync(user.Id, default);

            Assert.NotNull(expired);
            Assert.Equal("timeout", expired.Status);
            Assert.Null(expired.NextActionAtUtc);
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.Users.Where(value => value.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    private static readonly MutableTimeProvider Clock = new(DateTimeOffset.Parse("2026-10-11T00:00:00Z"));
    private static async Task<Guid> CreateLootTargetAsync(User user,
        IReadOnlyList<(Guid ItemId, decimal Chance, int Minimum, int Maximum)> rows,
        long minimumCredits = 2, long maximumCredits = 4)
    {
        await using var db = TestDatabase.CreateDbContext();
        var locationId = await db.HuntingLocationTargets.Where(value => value.TargetId == TargetId)
            .Select(value => value.LocationId).FirstAsync();
        var id = Guid.NewGuid();
        db.Users.Add(user);
        db.EquipmentInstances.Add(new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = user.Character!.Id,
            ItemId = MeleeId, Damage = 16, Accuracy = 100, EquippedSlot = EquipmentSlots.Melee, CreatedAtUtc = Clock.GetUtcNow() });
        db.HuntingTargets.Add(new HuntingTarget
        {
            Id = id, Name = "Тестовый робот добычи", Description = "Только тестовая база",
            MinimumHuntingLevel = 1, EnergyCost = 1, Health = 1,
            Strength = 1, Reflexes = 1, Toughness = 1, Agility = 1,
            WeaponName = "Тестовая лапа", WeaponDamage = 1, WeaponAccuracy = 0,
            Armor = 0, ArmorCoverage = "{}", RewardCredits = 5,
            MinimumCredits = minimumCredits, MaximumCredits = maximumCredits,
            RewardExperience = 3, RewardHuntingExperience = 2, IsActive = true,
            Locations = [new HuntingLocationTarget { LocationId = locationId }],
            LootRows = rows.Select((row, index) => new HuntingLootRow
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"),
                ItemId = row.ItemId, ChancePercent = row.Chance,
                MinimumQuantity = row.Minimum, MaximumQuantity = row.Maximum
            }).ToList()
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task DeleteLootFixtureAsync(Guid userId, Guid targetId)
    {
        await using var db = TestDatabase.CreateDbContext();
        await db.Users.Where(value => value.Id == userId).ExecuteDeleteAsync();
        await db.HuntingLocationTargets.Where(value => value.TargetId == targetId).ExecuteDeleteAsync();
        await db.HuntingTargets.Where(value => value.Id == targetId).ExecuteDeleteAsync();
    }

    private static HuntingService Service(GameDbContext db, ICombatCalculator? combat = null, ILootRoller? roller = null)
    {
        var options = Options.Create(Settings());
        return new(db, combat ?? new CombatCalculator(options, new ZeroRandom()), TestProgression.Create(),
            new ResourceRecoveryService(Options.Create(new ResourceRecoveryOptions { EnergyAmount = 5, EnergyIntervalMinutes = 15, NerveAmount = 1, NerveIntervalMinutes = 5 }), Clock),
            roller ?? new LootRoller(), new EquipmentRoller(), options);
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
    private sealed class SequenceLootRoller(params long[] values) : ILootRoller
    {
        private readonly Queue<long> values = new(values);
        public long NextInclusive(long minimum, long maximum)
        {
            var value = values.Dequeue();
            Assert.InRange(value, minimum, maximum);
            return value;
        }
    }
    private sealed class InstantVictoryCombat : ICombatCalculator
    {
        public CombatHit Attack(CombatStats attacker, CombatStats defender, CombatWeapon weapon, IReadOnlyList<CombatArmor> armor) =>
            new(true, "chest", 1, 0, 0, "Тестовый удар победил робота.");
    }
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
