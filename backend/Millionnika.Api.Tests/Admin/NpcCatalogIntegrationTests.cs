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
    public async Task LocationAssignments_KeepOneNpcRecordAndPreserveOriginalRobotAndLoot()
    {
        var admin = CreateAdmin();
        Guid robotId, originalLocation, otherLocation;
        await using (var setup = TestDatabase.CreateDbContext())
        {
            var locations = await setup.HuntingLocations.OrderBy(value => value.Id).Select(value => value.Id).ToListAsync();
            originalLocation = locations[0]; otherLocation = locations[1];
            robotId = await setup.HuntingLocationTargets.Where(value => value.LocationId == originalLocation)
                .Select(value => value.TargetId).FirstAsync();
            setup.Users.Add(admin);
            await setup.SaveChangesAsync();
        }
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var service = new HuntingAssignmentAdminService(db);
            var before = await db.HuntingTargets.AsNoTracking().Where(value => value.Id == robotId)
                .Select(value => new { value.Health, value.RewardCredits, value.RewardExperience,
                    value.RewardHuntingExperience }).SingleAsync();
            var lootCount = await db.HuntingLootRows.CountAsync(value => value.TargetId == robotId);
            await using var concurrentDb = TestDatabase.CreateDbContext();
            var attempts = await Task.WhenAll(
                service.AssignAsync(admin.Id, otherLocation, new(robotId, "Тест второго места", null), default),
                new HuntingAssignmentAdminService(concurrentDb).AssignAsync(admin.Id, otherLocation,
                    new(robotId, "Параллельное назначение", null), default));
            Assert.Equal(1, attempts.Count(value => value.Succeeded));
            Assert.False((await service.AssignAsync(admin.Id, otherLocation,
                new(robotId, "Повтор назначения", null), default)).Succeeded);
            Assert.Equal(2, await db.HuntingLocationTargets.CountAsync(value => value.TargetId == robotId));
            Assert.True((await service.RemoveAsync(admin.Id, otherLocation, robotId,
                new("Тест снятия"), default)).Succeeded);
            Assert.False((await service.RemoveAsync(admin.Id, otherLocation, robotId,
                new("Повтор снятия"), default)).Succeeded);
            Assert.True(await db.HuntingLocationTargets.AnyAsync(value => value.LocationId == originalLocation && value.TargetId == robotId));
            var after = await db.HuntingTargets.AsNoTracking().Where(value => value.Id == robotId)
                .Select(value => new { value.Health, value.RewardCredits, value.RewardExperience,
                    value.RewardHuntingExperience }).SingleAsync();
            Assert.Equal(before, after);
            Assert.Equal(lootCount, await db.HuntingLootRows.CountAsync(value => value.TargetId == robotId));
            Assert.Equal(2, await db.AdminAuditLogs.CountAsync(value => value.AdminUserId == admin.Id));
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.HuntingLocationTargets.Where(value => value.LocationId == otherLocation && value.TargetId == robotId).ExecuteDeleteAsync();
            await cleanup.AdminAuditLogs.Where(value => value.AdminUserId == admin.Id).ExecuteDeleteAsync();
            await cleanup.Users.Where(value => value.Id == admin.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task FirstAssignmentPromotesCombatTemplateOnceAndRejectsPeacefulNpc()
    {
        var admin = CreateAdmin();
        var combatId = Guid.NewGuid(); var peacefulId = Guid.NewGuid();
        await using (var setup = TestDatabase.CreateDbContext())
        {
            setup.Users.Add(admin);
            setup.NpcTemplates.Add(new NpcTemplate
            {
                Id = combatId, Name = "Тестовый мутант", Description = "Боевой НПЦ", Category = "mutant", IsActive = true,
                Health = 30, Strength = 2, Toughness = 2, Reflexes = 2, Agility = 2,
                WeaponName = "Лапа", WeaponDamage = 5, WeaponAccuracy = 50, Armor = 0, ArmorCoverage = "{}"
            });
            setup.NpcTemplates.Add(new NpcTemplate
            {
                Id = peacefulId, Name = "Мирный тест", Description = "Без боя", Category = "peaceful", IsActive = true
            });
            await setup.SaveChangesAsync();
        }
        try
        {
            await using var db = TestDatabase.CreateDbContext();
            var locations = await db.HuntingLocations.OrderBy(value => value.Id).Select(value => value.Id).ToListAsync();
            var service = new HuntingAssignmentAdminService(db);
            var setup = new HuntingSetupRequest(1, 1, 0, 0, 0, 0, 7, 0, 0, 2, 3);
            Assert.False((await service.AssignAsync(admin.Id, locations[0],
                new(peacefulId, "Мирный не подходит", setup), default)).Succeeded);
            Assert.False((await service.AssignAsync(admin.Id, locations[0],
                new(combatId, "Нужны настройки", null), default)).Succeeded);
            Assert.True((await service.AssignAsync(admin.Id, locations[0],
                new(combatId, "Первое назначение", setup), default)).Succeeded);
            Assert.True((await service.AssignAsync(admin.Id, locations[1],
                new(combatId, "Второе назначение", null), default)).Succeeded);
            Assert.False(await db.NpcTemplates.AnyAsync(value => value.Id == combatId));
            Assert.Equal(1, await db.HuntingTargets.CountAsync(value => value.Id == combatId));
            Assert.Equal(2, await db.HuntingLocationTargets.CountAsync(value => value.TargetId == combatId));
            var npc = Assert.Single(await new NpcAdminService(db).GetAllAsync(default), value => value.Id == combatId);
            Assert.Equal(("hunting", "mutant", 30), (npc.Source, npc.Category, npc.Health));
            Assert.Equal(3, await db.AdminAuditLogs.CountAsync(value => value.AdminUserId == admin.Id));
        }
        finally
        {
            await using var cleanup = TestDatabase.CreateDbContext();
            await cleanup.HuntingLocationTargets.Where(value => value.TargetId == combatId).ExecuteDeleteAsync();
            await cleanup.AdminAuditLogs.Where(value => value.AdminUserId == admin.Id).ExecuteDeleteAsync();
            await cleanup.HuntingTargets.Where(value => value.Id == combatId).ExecuteDeleteAsync();
            await cleanup.NpcTemplates.Where(value => value.Id == combatId || value.Id == peacefulId).ExecuteDeleteAsync();
            await cleanup.Users.Where(value => value.Id == admin.Id).ExecuteDeleteAsync();
        }
    }

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
        var assignments = await db.HuntingLocationTargets.AsNoTracking()
            .OrderBy(value => value.TargetId).Select(value => new { value.TargetId, value.LocationId }).ToListAsync();
        Assert.Equal(3, assignments.Count);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444001"), assignments[0].LocationId);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444001"), assignments[1].LocationId);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444002"), assignments[2].LocationId);
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
                Id = id, Name = "Тестовый робот НПЦ", Description = "Тест",
                MinimumHuntingLevel = 1, EnergyCost = 12, Health = 30,
                Strength = 2, Toughness = 2, Reflexes = 2, Agility = 2,
                WeaponName = "Лапа", WeaponDamage = 5, WeaponAccuracy = 50,
                Armor = 0, ArmorCoverage = "{}", RewardCredits = 45,
                RewardExperience = 8, RewardHuntingExperience = 15, IsActive = false,
                Locations = [new HuntingLocationTarget { LocationId = location }]
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
            await cleanup.HuntingLocationTargets.Where(value => value.TargetId == id).ExecuteDeleteAsync();
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
