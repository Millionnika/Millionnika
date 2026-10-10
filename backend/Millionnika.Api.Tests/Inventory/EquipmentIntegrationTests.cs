using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Inventory;
using Millionnika.Api.Resources;
using Millionnika.Api.Tests.Infrastructure;
using Xunit;

namespace Millionnika.Api.Tests.Inventory;

[Collection("PostgreSQL integration")]
public sealed class EquipmentIntegrationTests
{
    private static readonly Guid PrimaryId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid SecondaryId = Guid.Parse("22222222-2222-2222-2222-222222222002");
    private static readonly Guid HelmetId = Guid.Parse("22222222-2222-2222-2222-222222222004");

    [Fact]
    public async Task Purchase_CreatesUniqueInstances_AndReplayDoesNotDuplicate()
    {
        await WithPlayersAsync(async (owner, _) =>
        {
            var requestId = Guid.NewGuid();
            await using (var db = CreateDbContext())
            {
                var result = await CreateService(db).BuyAsync(owner.UserId, requestId, PrimaryId, 2, default);
                Assert.True(result.Response.Succeeded);
            }
            await using (var db = CreateDbContext())
            {
                var replay = await CreateService(db).BuyAsync(owner.UserId, requestId, PrimaryId, 2, default);
                Assert.True(replay.Response.Replayed);
                var conflict = await CreateService(db).BuyAsync(owner.UserId, requestId, PrimaryId, 1, default);
                Assert.True(conflict.Rejected);
            }
            await using var verify = CreateDbContext();
            var instances = await verify.EquipmentInstances.Where(value => value.CharacterId == owner.CharacterId).ToListAsync();
            Assert.Equal(2, instances.Count);
            Assert.All(instances, value => { Assert.Equal(56, value.Damage); Assert.Equal(52, value.Accuracy); });
            Assert.Equal(400, await verify.Characters.Where(value => value.Id == owner.CharacterId).Select(value => value.Credits).SingleAsync());
        });
    }

    [Fact]
    public async Task Equip_ReplacesAndUnequips_WhileRejectingWrongOrForeignInstance()
    {
        await WithPlayersAsync(async (owner, other) =>
        {
            var first = await AddInstanceAsync(owner.CharacterId, PrimaryId);
            var replacement = await AddInstanceAsync(owner.CharacterId, PrimaryId);
            var helmet = await AddInstanceAsync(owner.CharacterId, HelmetId);
            var foreign = await AddInstanceAsync(other.CharacterId, PrimaryId);

            await using (var db = CreateDbContext()) Assert.True((await CreateService(db).EquipAsync(owner.UserId, Guid.NewGuid(), first, EquipmentSlots.Primary, default)).Response.Succeeded);
            await using (var db = CreateDbContext()) Assert.True((await CreateService(db).EquipAsync(owner.UserId, Guid.NewGuid(), replacement, EquipmentSlots.Primary, default)).Response.Succeeded);
            await using (var db = CreateDbContext()) Assert.True((await CreateService(db).EquipAsync(owner.UserId, Guid.NewGuid(), helmet, EquipmentSlots.Primary, default)).Rejected);
            await using (var db = CreateDbContext()) Assert.True((await CreateService(db).EquipAsync(owner.UserId, Guid.NewGuid(), foreign, EquipmentSlots.Primary, default)).Rejected);

            await using (var verify = CreateDbContext())
            {
                Assert.Null((await verify.EquipmentInstances.FindAsync(first))!.EquippedSlot);
                Assert.Equal(EquipmentSlots.Primary, (await verify.EquipmentInstances.FindAsync(replacement))!.EquippedSlot);
            }
            await using (var db = CreateDbContext()) Assert.True((await CreateService(db).UnequipAsync(owner.UserId, Guid.NewGuid(), EquipmentSlots.Primary, default)).Response.Succeeded);
            await using var final = CreateDbContext();
            Assert.Empty(await final.EquipmentInstances.Where(value => value.CharacterId == owner.CharacterId && value.EquippedSlot != null).ToListAsync());
        });
    }

    [Fact]
    public async Task ParallelPurchaseAndEquip_StayConsistent()
    {
        await WithPlayersAsync(async (owner, _) =>
        {
            var requestId = Guid.NewGuid();
            await using var firstDb = CreateDbContext(); await using var secondDb = CreateDbContext();
            await Task.WhenAll(
                CreateService(firstDb).BuyAsync(owner.UserId, requestId, SecondaryId, 1, default),
                CreateService(secondDb).BuyAsync(owner.UserId, requestId, SecondaryId, 1, default));
            Guid instance;
            await using (var verify = CreateDbContext())
            {
                instance = await verify.EquipmentInstances.Where(value => value.CharacterId == owner.CharacterId).Select(value => value.Id).SingleAsync();
                Assert.Equal(900, await verify.Characters.Where(value => value.Id == owner.CharacterId).Select(value => value.Credits).SingleAsync());
            }
            await using var thirdDb = CreateDbContext(); await using var fourthDb = CreateDbContext();
            await Task.WhenAll(
                CreateService(thirdDb).EquipAsync(owner.UserId, Guid.NewGuid(), instance, EquipmentSlots.Secondary, default),
                CreateService(fourthDb).EquipAsync(owner.UserId, Guid.NewGuid(), instance, EquipmentSlots.Secondary, default));
            await using var final = CreateDbContext();
            Assert.Equal(1, await final.EquipmentInstances.CountAsync(value => value.CharacterId == owner.CharacterId && value.EquippedSlot == EquipmentSlots.Secondary));
        });
    }

    [Fact]
    public async Task DefinitionRangeChange_DoesNotRerollExistingInstance()
    {
        await WithPlayersAsync(async (owner, _) =>
        {
            var instanceId = await AddInstanceAsync(owner.CharacterId, PrimaryId);
            await using (var db = CreateDbContext())
            {
                var definition = await db.Items.SingleAsync(value => value.Id == PrimaryId);
                definition.MinimumDamage = 55; definition.MaximumDamage = 57;
                await db.SaveChangesAsync();
            }
            try
            {
                await using var verify = CreateDbContext();
                var instance = await verify.EquipmentInstances.SingleAsync(value => value.Id == instanceId);
                Assert.Equal(56, instance.Damage);
            }
            finally
            {
                await using var restore = CreateDbContext();
                var definition = await restore.Items.SingleAsync(value => value.Id == PrimaryId);
                definition.MinimumDamage = 56; definition.MaximumDamage = 56;
                await restore.SaveChangesAsync();
            }
        });
    }

    [Fact]
    public async Task Deactivation_HidesShopButPreservesOwnedEquipment()
    {
        await WithPlayersAsync(async (owner, _) =>
        {
            await AddInstanceAsync(owner.CharacterId, HelmetId);
            await using (var db = CreateDbContext())
            {
                var definition = await db.Items.SingleAsync(value => value.Id == HelmetId);
                definition.IsActive = false; await db.SaveChangesAsync();
            }
            try
            {
                await using var db = CreateDbContext(); var service = CreateService(db);
                Assert.DoesNotContain(await service.GetShopAsync(default), value => value.Id == HelmetId);
                Assert.Contains(await service.GetInventoryAsync(owner.UserId, default), value => value.ItemId == HelmetId);
            }
            finally
            {
                await using var restore = CreateDbContext();
                var definition = await restore.Items.SingleAsync(value => value.Id == HelmetId);
                definition.IsActive = true; await restore.SaveChangesAsync();
            }
        });
    }

    private static async Task<Guid> AddInstanceAsync(Guid characterId, Guid itemId)
    {
        await using var db = CreateDbContext(); var item = await db.Items.SingleAsync(value => value.Id == itemId);
        var instance = new EquipmentInstance { Id = Guid.NewGuid(), CharacterId = characterId, ItemId = itemId,
            Damage = item.MinimumDamage, Accuracy = item.MinimumAccuracy, Armor = item.MinimumArmor, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.EquipmentInstances.Add(instance); await db.SaveChangesAsync(); return instance.Id;
    }

    private static async Task WithPlayersAsync(Func<TestPlayer, TestPlayer, Task> test)
    {
        var first = CreateUser(1000); var second = CreateUser(1000);
        await using (var setup = CreateDbContext()) { setup.Users.AddRange(first, second); await setup.SaveChangesAsync(); }
        try { await test(new(first.Id, first.Character!.Id), new(second.Id, second.Character!.Id)); }
        finally { await using var cleanup = CreateDbContext(); await cleanup.Users.Where(value => value.Id == first.Id || value.Id == second.Id).ExecuteDeleteAsync(); }
    }

    private static User CreateUser(long credits)
    {
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        return new User { Id = id, Username = $"Equip{id:N}"[..20], NormalizedUsername = $"EQUIP{id:N}"[..20], Email = $"{id:N}@equipment.invalid", NormalizedEmail = $"{id:N}@EQUIPMENT.INVALID", PasswordHash = "integration-test-not-a-real-password", CreatedAtUtc = now,
            Character = new GameCharacter { Id = Guid.NewGuid(), UserId = id, Level = 1, Credits = credits, Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now, Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = now, Health = 100, MaxHealth = 100, Strength = 1, Speed = 1, Defense = 1, Dexterity = 1 } };
    }

    private static InventoryService CreateService(GameDbContext db) => new(db,
        new ResourceRecoveryService(Options.Create(new ResourceRecoveryOptions { EnergyAmount = 5, EnergyIntervalMinutes = 15, NerveAmount = 1, NerveIntervalMinutes = 5 }), TimeProvider.System), new EquipmentRoller());
    private static GameDbContext CreateDbContext() => TestDatabase.CreateDbContext();
    private sealed record TestPlayer(Guid UserId, Guid CharacterId);
}
