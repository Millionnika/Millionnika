using Millionnika.Api.Tests.Infrastructure;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Players;
using Xunit;

namespace Millionnika.Api.Tests.Players;

[Collection("PostgreSQL integration")]
public sealed class PlayerDirectoryIntegrationTests
{
    [Fact]
    public async Task Search_IsStableAndPaginatesByTwenty()
    {
        var prefix = $"D{Guid.NewGuid():N}"[..7];
        var users = Enumerable.Range(0, 25).Select(index =>
            CreateUser($"{prefix}_{index:00}", false, string.Empty)).ToList();
        await WithUsersAsync(users, async service =>
        {
            var first = await service.SearchAsync(prefix, 1, 20, default);
            var second = await service.SearchAsync(prefix, 2, 20, default);
            Assert.Equal(20, first.Players.Count);
            Assert.Equal(5, second.Players.Count);
            Assert.Equal(25, first.TotalCount);
            Assert.Equal(2, first.TotalPages);
            Assert.Equal($"{prefix}_00", first.Players[0].Username);
            Assert.Equal($"{prefix}_20", second.Players[0].Username);
        });
    }

    [Fact]
    public async Task BlockedPlayer_IsHiddenFromSearchAndDirectProfile()
    {
        var user = CreateUser($"Hidden{Guid.NewGuid():N}"[..16], true, "Скрыт");
        await WithUsersAsync([user], async service =>
        {
            var search = await service.SearchAsync(user.Username, 1, 20, default);
            var profile = await service.GetProfileAsync(Guid.NewGuid(), user.Id, default);
            Assert.Empty(search.Players);
            Assert.Null(profile);
        });
    }

    [Fact]
    public async Task PublicProfile_HasNoPrivateFieldsAndKeepsHtmlAsText()
    {
        const string description = "<script>alert('x')</script><b>Текст</b>";
        var user = CreateUser($"Public{Guid.NewGuid():N}"[..16], false, description, "Admin");
        await WithUsersAsync([user], async service =>
        {
            var profile = await service.GetProfileAsync(Guid.NewGuid(), user.Id, default);
            Assert.NotNull(profile);
            Assert.Equal(description, profile.Description);
            var json = JsonSerializer.Serialize(profile);
            Assert.DoesNotContain("Email", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Credits", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Role", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Strength", json, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Description_CanOnlyBeChangedByOwner()
    {
        var owner = CreateUser($"Owner{Guid.NewGuid():N}"[..16], false, "Старое");
        var stranger = CreateUser($"Other{Guid.NewGuid():N}"[..16], false, string.Empty);
        await WithUsersAsync([owner, stranger], async service =>
        {
            var result = await service.UpdateDescriptionAsync(stranger.Id, owner.Id, "Чужое", default);
            Assert.False(result.Succeeded);
            await using var db = CreateDbContext();
            Assert.Equal("Старое", (await db.Users.SingleAsync(value => value.Id == owner.Id)).Description);
        });
    }

    [Fact]
    public async Task Description_RejectsLongValue()
    {
        var owner = CreateUser($"Length{Guid.NewGuid():N}"[..16], false, string.Empty);
        await WithUsersAsync([owner], async service =>
        {
            var result = await service.UpdateDescriptionAsync(owner.Id, owner.Id, new string('я', 301), default);
            Assert.False(result.Succeeded);
        });
    }

    [Fact]
    public async Task Owner_CanSaveHtmlLookingDescriptionAsPlainText()
    {
        const string text = "<img src=x onerror=alert(1)>";
        var owner = CreateUser($"Markup{Guid.NewGuid():N}"[..16], false, string.Empty);
        await WithUsersAsync([owner], async service =>
        {
            var result = await service.UpdateDescriptionAsync(owner.Id, owner.Id, text, default);
            var profile = await service.GetProfileAsync(owner.Id, owner.Id, default);
            Assert.True(result.Succeeded);
            Assert.Equal(text, profile!.Description);
            Assert.True(profile.CanEdit);
        });
    }

    private static async Task WithUsersAsync(IReadOnlyCollection<User> users, Func<PlayerDirectoryService, Task> test)
    {
        await using (var setup = CreateDbContext())
        {
            setup.Users.AddRange(users);
            await setup.SaveChangesAsync();
        }
        try
        {
            await using var action = CreateDbContext();
            await test(new PlayerDirectoryService(action));
        }
        finally
        {
            var ids = users.Select(user => user.Id).ToArray();
            await using var cleanup = CreateDbContext();
            await cleanup.Users.Where(user => ids.Contains(user.Id)).ExecuteDeleteAsync();
        }
    }

    private static User CreateUser(string username, bool blocked, string description, string role = "Player")
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = id, Username = username, NormalizedUsername = username.ToUpperInvariant(),
            Email = $"{id:N}@directory.invalid", NormalizedEmail = $"{id:N}@DIRECTORY.INVALID",
            PasswordHash = "integration-test-not-a-real-password", Role = role,
            IsBlocked = blocked, Description = description, CreatedAtUtc = now,
            Character = new GameCharacter
            {
                Id = Guid.NewGuid(), UserId = id, Level = 3, Credits = 999,
                Energy = 80, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now,
                Nerve = 8, MaxNerve = 10, LastNerveRecoveryAtUtc = now,
                Health = 70, MaxHealth = 100, Strength = 9, Reflexes = 8, Toughness = 7, Agility = 6
            }
        };
    }

    private static GameDbContext CreateDbContext() => TestDatabase.CreateDbContext();

}
