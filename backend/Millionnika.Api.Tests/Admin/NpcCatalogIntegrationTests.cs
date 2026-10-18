using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Admin;
using Millionnika.Api.Entities;
using Millionnika.Api.Tests.Infrastructure;
using Xunit;

namespace Millionnika.Api.Tests.Admin;

[Collection("PostgreSQL integration")]
public sealed class NpcCatalogIntegrationTests
{
    [Fact]
    public async Task ExistingHuntingRobotsAppearWithoutCopiesOrChangedCombatValues()
    {
        await using var db = TestDatabase.CreateDbContext();
        var before = await db.HuntingTargets.AsNoTracking().OrderBy(value => value.Id)
            .Select(value => new { value.Id, value.Name, value.Health, value.RewardCredits, value.RewardExperience })
            .ToListAsync();
        var catalog = await new NpcAdminService(db).GetAllAsync(default);
        Assert.Equal(3, before.Count);
        Assert.Equal(before.Select(value => value.Id), catalog.Where(value => value.Source == "hunting")
            .OrderBy(value => value.Id).Select(value => value.Id));
        Assert.All(catalog.Where(value => value.Source == "hunting"), value => Assert.Equal("robot", value.Category));
        var after = await db.HuntingTargets.AsNoTracking().OrderBy(value => value.Id)
            .Select(value => new { value.Id, value.Name, value.Health, value.RewardCredits, value.RewardExperience })
            .ToListAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task CreateAndEditTemplates_ValidateCombatAndImagesAndWriteAudit()
    {
        var admin = CreateAdmin();
        var peacefulName = $"Мирный-{Guid.NewGuid():N}";
        var robotName = $"Робот-{Guid.NewGuid():N}";
        await using (var setup = TestDatabase.CreateDbContext())
        {
            setup.Users.Add(admin);
            await setup.SaveChangesAsync();
        }
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var service = new NpcAdminService(db);
            var peaceful = new SaveNpcRequest("peaceful", peacefulName, "Без боевых параметров", true,
                null, null, null, null, null, null, null, null, null, null, null, null, "Тест каталога");
            Assert.True((await service.CreateAsync(admin.Id, peaceful, default)).Succeeded);
            var robot = new SaveNpcRequest("robot", robotName, "Пока не назначен в охоту", false,
                "/images/robot.png", null, 40, 2, 2, 3, 3, "Манипулятор", 5, 50, 0, "{}", "Тест каталога");
            Assert.False((await service.CreateAsync(admin.Id, robot with { Health = null }, default)).Succeeded);
            Assert.False((await service.CreateAsync(admin.Id, robot with { PortraitPath = "javascript:alert(1)" }, default)).Succeeded);
            Assert.True((await service.CreateAsync(admin.Id, robot, default)).Succeeded);
            var created = Assert.Single(await service.GetAllAsync(default), value => value.Name == robotName);
            Assert.Equal("template", created.Source);
            Assert.Equal(40, created.Health);
            Assert.False(created.IsActive);
            Assert.True((await service.UpdateAsync(admin.Id, created.Id, robot with
            { Name = robotName + "-2", FigurePath = "/images/robot-full.png", IsActive = true }, default)).Succeeded);
            var edited = Assert.Single(await service.GetAllAsync(default), value => value.Id == created.Id);
            Assert.Equal("/images/robot-full.png", edited.FigurePath);
            Assert.True(edited.IsActive);
            Assert.Single(await service.GetAllAsync(default), value => value.Name == peacefulName && value.Health is null);
            Assert.Equal(3, await db.AdminAuditLogs.CountAsync(value => value.AdminUserId == admin.Id));
            Assert.Equal(0, await db.HuntingTargets.CountAsync(value => value.Name.StartsWith(robotName)));
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.AdminAuditLogs.Where(value => value.AdminUserId == admin.Id).ExecuteDeleteAsync();
            await cleanup.NpcTemplates.Where(value => value.Name == peacefulName || value.Name.StartsWith(robotName)).ExecuteDeleteAsync();
            await cleanup.Users.Where(value => value.Id == admin.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task EditingHuntingRobot_UsesSameRowAndPreservesHuntingRewards()
    {
        var admin = CreateAdmin();
        var id = Guid.NewGuid();
        await using (var setup = TestDatabase.CreateDbContext())
        {
            var location = await setup.HuntingLocations.Select(value => value.Id).FirstAsync();
            setup.Users.Add(admin);
            setup.HuntingTargets.Add(new HuntingTarget
            {
                Id = id, LocationId = location, Name = "Тестовый робот НПЦ", Description = "Тест",
                MinimumHuntingLevel = 1, EnergyCost = 12, Health = 30,
                Strength = 2, Toughness = 2, Reflexes = 2, Agility = 2,
                WeaponName = "Лапа", WeaponDamage = 5, WeaponAccuracy = 50,
                Armor = 0, ArmorCoverage = "{}", RewardCredits = 45,
                RewardExperience = 8, RewardHuntingExperience = 15, IsActive = false
            });
            await setup.SaveChangesAsync();
        }
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var service = new NpcAdminService(db);
            var request = new SaveNpcRequest("robot", "Робот НПЦ обновлён", "Новый текст", false,
                null, "/images/robot-full.png", 50, 3, 3, 3, 3, "Новая лапа", 6, 51, 0, "{}", "Проверка общей записи");
            Assert.False((await service.UpdateAsync(admin.Id, id, request with { Category = "animal" }, default)).Succeeded);
            Assert.True((await service.UpdateAsync(admin.Id, id, request, default)).Succeeded);
            var record = await db.HuntingTargets.AsNoTracking().SingleAsync(value => value.Id == id);
            Assert.Equal("Робот НПЦ обновлён", record.Name);
            Assert.Equal(50, record.Health);
            Assert.Equal((12, 45, 8, 15),
                (record.EnergyCost, record.RewardCredits, record.RewardExperience, record.RewardHuntingExperience));
            Assert.Equal(0, await db.NpcTemplates.CountAsync(value => value.Id == id));
            Assert.Single(await db.AdminAuditLogs.Where(value => value.AdminUserId == admin.Id && value.TargetId == id.ToString()).ToListAsync());
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.AdminAuditLogs.Where(value => value.AdminUserId == admin.Id).ExecuteDeleteAsync();
            await cleanup.HuntingTargets.Where(value => value.Id == id).ExecuteDeleteAsync();
            await cleanup.Users.Where(value => value.Id == admin.Id).ExecuteDeleteAsync();
        }
    }

    private static User CreateAdmin()
    {
        var id = Guid.NewGuid();
        return new User
        {
            Id = id, Username = $"Npc{id:N}"[..20], NormalizedUsername = $"NPC{id:N}"[..20],
            Email = $"{id:N}@npc.invalid", NormalizedEmail = $"{id:N}@NPC.INVALID",
            PasswordHash = "test-only", Role = "Admin", CreatedAtUtc = DateTimeOffset.UtcNow,
            Character = new GameCharacter
            {
                Id = Guid.NewGuid(), UserId = id, Level = 1, Credits = 0,
                Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = DateTimeOffset.UtcNow,
                Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = DateTimeOffset.UtcNow,
                Health = 100, MaxHealth = 100, Strength = 1, Toughness = 1, Reflexes = 1, Agility = 1
            }
        };
    }
}
