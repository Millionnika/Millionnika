using Millionnika.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Friends;
using Xunit;

namespace Millionnika.Api.Tests.Friends;

[Collection("PostgreSQL integration")]
public sealed class FriendServiceIntegrationTests
{
    [Fact]
    public async Task Request_Accept_Friendship_Remove_MainPath()
    {
        var users = CreateUsers(2);
        await WithUsersAsync(users, async () =>
        {
            var sent = await Call(service => service.SendRequestAsync(users[0].Id, users[1].Id, default));
            Assert.True(sent.Succeeded);
            var relationId = Assert.IsType<Guid>(sent.RelationId);
            var accepted = await Call(service => service.AcceptAsync(users[1].Id, relationId, default));
            Assert.True(accepted.Succeeded);
            Assert.Equal("friends", accepted.State);

            var list = await Call(service => service.GetListAsync(users[0].Id, "friends", 1, 20, default));
            Assert.Single(list.Items);
            Assert.Equal(users[1].Id, list.Items[0].PlayerId);

            var removed = await Call(service => service.RemoveAsync(users[0].Id, relationId, default));
            Assert.True(removed.Succeeded);
            Assert.Empty((await Call(service => service.GetListAsync(users[1].Id, "friends", 1, 20, default))).Items);
        });
    }

    [Fact]
    public async Task Sender_CanCancelRequest()
    {
        var users = CreateUsers(2);
        await WithUsersAsync(users, async () =>
        {
            var sent = await Call(service => service.SendRequestAsync(users[0].Id, users[1].Id, default));
            Assert.True((await Call(service => service.CancelAsync(users[0].Id, sent.RelationId!.Value, default))).Succeeded);
            Assert.Equal("none", (await Call(service => service.GetStatusAsync(users[0].Id, users[1].Id, default))).State);
        });
    }

    [Fact]
    public async Task Recipient_CanRejectRequest()
    {
        var users = CreateUsers(2);
        await WithUsersAsync(users, async () =>
        {
            var sent = await Call(service => service.SendRequestAsync(users[0].Id, users[1].Id, default));
            Assert.True((await Call(service => service.RejectAsync(users[1].Id, sent.RelationId!.Value, default))).Succeeded);
            Assert.Equal("none", (await Call(service => service.GetStatusAsync(users[1].Id, users[0].Id, default))).State);
        });
    }

    [Fact]
    public async Task SelfRequest_IsRejected()
    {
        var users = CreateUsers(1);
        await WithUsersAsync(users, async () =>
        {
            var result = await Call(service => service.SendRequestAsync(users[0].Id, users[0].Id, default));
            Assert.False(result.Succeeded);
            Assert.Equal("self_request", result.ErrorCode);
        });
    }

    [Fact]
    public async Task OnlyCorrectParty_CanActOnRequestOrFriendship()
    {
        var users = CreateUsers(3);
        await WithUsersAsync(users, async () =>
        {
            var sent = await Call(service => service.SendRequestAsync(users[0].Id, users[1].Id, default));
            var relationId = Assert.IsType<Guid>(sent.RelationId);
            Assert.Equal("forbidden", (await Call(service => service.AcceptAsync(users[0].Id, relationId, default))).ErrorCode);
            Assert.Equal("forbidden", (await Call(service => service.CancelAsync(users[1].Id, relationId, default))).ErrorCode);
            Assert.True((await Call(service => service.AcceptAsync(users[1].Id, relationId, default))).Succeeded);
            Assert.Equal("forbidden", (await Call(service => service.RemoveAsync(users[2].Id, relationId, default))).ErrorCode);
        });
    }

    [Fact]
    public async Task ParallelDuplicateRequests_CreateOneRow()
    {
        var users = CreateUsers(2);
        await WithUsersAsync(users, async () =>
        {
            var results = await Task.WhenAll(
                Call(service => service.SendRequestAsync(users[0].Id, users[1].Id, default)),
                Call(service => service.SendRequestAsync(users[0].Id, users[1].Id, default)));
            Assert.All(results, result => Assert.True(result.Succeeded));
            Assert.Equal(results[0].RelationId, results[1].RelationId);
            await using var db = CreateDbContext();
            Assert.Equal(1, await db.FriendRelations.CountAsync(value =>
                value.UserLowId == FriendRules.CanonicalPair(users[0].Id, users[1].Id).Low &&
                value.UserHighId == FriendRules.CanonicalPair(users[0].Id, users[1].Id).High));
        });
    }

    [Fact]
    public async Task ParallelCrossRequests_KeepOnePendingRequestAndShowIncoming()
    {
        var users = CreateUsers(2);
        await WithUsersAsync(users, async () =>
        {
            var results = await Task.WhenAll(
                Call(service => service.SendRequestAsync(users[0].Id, users[1].Id, default)),
                Call(service => service.SendRequestAsync(users[1].Id, users[0].Id, default)));
            Assert.Single(results, result => result.Succeeded);
            Assert.Contains(results, result => result.State == "incoming");
            await using var db = CreateDbContext();
            var relation = await db.FriendRelations.SingleAsync(value =>
                value.UserLowId == FriendRules.CanonicalPair(users[0].Id, users[1].Id).Low &&
                value.UserHighId == FriendRules.CanonicalPair(users[0].Id, users[1].Id).High);
            Assert.Equal("Pending", relation.Status);
            var recipientStatus = await Call(service => service.GetStatusAsync(relation.RecipientId, relation.SenderId, default));
            Assert.Equal("incoming", recipientStatus.State);
        });
    }

    [Fact]
    public async Task BlockedPlayer_IsHiddenAndUnavailableForNewAction()
    {
        var users = CreateUsers(3);
        await WithUsersAsync(users, async () =>
        {
            var sent = await Call(service => service.SendRequestAsync(users[0].Id, users[1].Id, default));
            await using (var db = CreateDbContext())
            {
                await db.Users.Where(user => user.Id == users[1].Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsBlocked, true));
            }
            Assert.Empty((await Call(service => service.GetListAsync(users[0].Id, "outgoing", 1, 20, default))).Items);
            Assert.Equal("player_unavailable", (await Call(service => service.AcceptAsync(users[1].Id, sent.RelationId!.Value, default))).ErrorCode);

            await using (var db = CreateDbContext())
            {
                await db.Users.Where(user => user.Id == users[2].Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsBlocked, true));
            }
            Assert.Equal("player_unavailable", (await Call(service => service.SendRequestAsync(users[0].Id, users[2].Id, default))).ErrorCode);
        });
    }

    [Fact]
    public async Task RemovingOnlyItemOnPageTwo_ReturnsExistingPage()
    {
        var users = CreateUsers(22);
        await WithUsersAsync(users, async () =>
        {
            var now = DateTimeOffset.UtcNow;
            await using (var setup = CreateDbContext())
            {
                for (var index = 1; index < users.Count; index++)
                {
                    var (low, high) = FriendRules.CanonicalPair(users[0].Id, users[index].Id);
                    setup.FriendRelations.Add(new FriendRelation
                    {
                        Id = Guid.NewGuid(), UserLowId = low, UserHighId = high,
                        SenderId = users[0].Id, RecipientId = users[index].Id,
                        Status = "Accepted", CreatedAtUtc = now, UpdatedAtUtc = now
                    });
                }
                await setup.SaveChangesAsync();
            }

            var secondPage = await Call(service => service.GetListAsync(users[0].Id, "friends", 2, 20, default));
            Assert.Single(secondPage.Items);
            Assert.True((await Call(service => service.RemoveAsync(
                users[0].Id, secondPage.Items[0].RelationId, default))).Succeeded);

            var corrected = await Call(service => service.GetListAsync(users[0].Id, "friends", 2, 20, default));
            Assert.Equal(1, corrected.Page);
            Assert.Equal(1, corrected.TotalPages);
            Assert.Equal(20, corrected.Items.Count);
        });
    }

    private static async Task<T> Call<T>(Func<FriendService, Task<T>> action)
    {
        await using var db = CreateDbContext();
        return await action(new FriendService(db, TimeProvider.System));
    }

    private static async Task WithUsersAsync(IReadOnlyCollection<User> users, Func<Task> test)
    {
        await using (var setup = CreateDbContext())
        {
            setup.Users.AddRange(users);
            await setup.SaveChangesAsync();
        }
        try { await test(); }
        finally
        {
            var ids = users.Select(user => user.Id).ToArray();
            await using var cleanup = CreateDbContext();
            await cleanup.FriendRelations.Where(value =>
                ids.Contains(value.UserLowId) || ids.Contains(value.UserHighId)).ExecuteDeleteAsync();
            await cleanup.Users.Where(user => ids.Contains(user.Id)).ExecuteDeleteAsync();
        }
    }

    private static List<User> CreateUsers(int count) => Enumerable.Range(0, count)
        .Select(index => CreateUser($"Friend{index}{Guid.NewGuid():N}"[..20])).ToList();

    private static User CreateUser(string username)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = id, Username = username, NormalizedUsername = username.ToUpperInvariant(),
            Email = $"{id:N}@friends.invalid", NormalizedEmail = $"{id:N}@FRIENDS.INVALID",
            PasswordHash = "integration-test-not-a-real-password", Role = "Player",
            Description = string.Empty, CreatedAtUtc = now,
            Character = new GameCharacter
            {
                Id = Guid.NewGuid(), UserId = id, Level = 1, Credits = 100,
                Energy = 100, MaxEnergy = 100, LastEnergyRecoveryAtUtc = now,
                Nerve = 10, MaxNerve = 10, LastNerveRecoveryAtUtc = now,
                Health = 100, MaxHealth = 100, Strength = 1, Speed = 1, Defense = 1, Dexterity = 1
            }
        };
    }

    private static GameDbContext CreateDbContext() => TestDatabase.CreateDbContext();

}
