using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;

namespace Millionnika.Api.Friends;

public sealed class FriendService(GameDbContext dbContext, TimeProvider timeProvider) : IFriendService
{
    public async Task<FriendStatusResponse> GetStatusAsync(
        Guid userId, Guid playerId, CancellationToken cancellationToken)
    {
        if (userId == playerId) return new("self", null);
        var targetAvailable = await dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == playerId && !user.IsBlocked, cancellationToken);
        if (!targetAvailable) return new("unavailable", null);

        var (low, high) = FriendRules.CanonicalPair(userId, playerId);
        var relation = await dbContext.FriendRelations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserLowId == low && value.UserHighId == high, cancellationToken);
        if (relation is null) return new("none", null);
        if (relation.Status == "Accepted") return new("friends", relation.Id);
        return relation.SenderId == userId
            ? new("outgoing", relation.Id)
            : new("incoming", relation.Id);
    }

    public async Task<FriendListResponse> GetListAsync(
        Guid userId, string list, int page, int pageSize, CancellationToken cancellationToken)
    {
        var relations = dbContext.FriendRelations.AsNoTracking().Where(relation =>
            relation.UserLowId == userId || relation.UserHighId == userId);
        relations = list switch
        {
            "friends" => relations.Where(relation => relation.Status == "Accepted"),
            "incoming" => relations.Where(relation => relation.Status == "Pending" && relation.RecipientId == userId),
            _ => relations.Where(relation => relation.Status == "Pending" && relation.SenderId == userId)
        };

        var visible = relations.Select(relation => new
        {
            Relation = relation,
            PlayerId = relation.UserLowId == userId ? relation.UserHighId : relation.UserLowId,
            Username = relation.UserLowId == userId ? relation.UserHigh.Username : relation.UserLow.Username,
            Level = relation.UserLowId == userId ? relation.UserHigh.Character.Level : relation.UserLow.Character.Level,
            IsBlocked = relation.UserLowId == userId ? relation.UserHigh.IsBlocked : relation.UserLow.IsBlocked
        }).Where(value => !value.IsBlocked);

        var totalCount = await visible.CountAsync(cancellationToken);
        var items = await visible
            .OrderBy(value => value.Username)
            .ThenBy(value => value.PlayerId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(value => new FriendListItem(
                value.Relation.Id,
                value.PlayerId,
                value.Username,
                value.Level,
                value.Relation.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<FriendOperationResult> SendRequestAsync(
        Guid userId, Guid playerId, CancellationToken cancellationToken)
    {
        if (userId == playerId)
            return FriendOperationResult.Failure("Нельзя отправить заявку самому себе.", "self", "self_request");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var users = await LockUsersAsync(userId, playerId, cancellationToken);
        if (users.Count != 2 || users.Any(user => user.IsBlocked))
            return FriendOperationResult.Failure("Игрок недоступен.", "unavailable", "player_unavailable");

        var (low, high) = FriendRules.CanonicalPair(userId, playerId);
        var existing = await dbContext.FriendRelations
            .SingleOrDefaultAsync(value => value.UserLowId == low && value.UserHighId == high, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == "Accepted")
                return FriendOperationResult.Failure("Этот игрок уже в друзьях.", "friends", "already_friends", existing.Id);
            if (existing.SenderId == userId)
                return FriendOperationResult.Success("Заявка уже отправлена.", "outgoing", existing.Id);
            return FriendOperationResult.Failure(
                "У вас уже есть входящая заявка от этого игрока.", "incoming", "incoming_exists", existing.Id);
        }

        var now = timeProvider.GetUtcNow();
        var relation = new FriendRelation
        {
            Id = Guid.NewGuid(), UserLowId = low, UserHighId = high,
            SenderId = userId, RecipientId = playerId, Status = "Pending",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        dbContext.FriendRelations.Add(relation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendOperationResult.Success("Заявка отправлена.", "outgoing", relation.Id);
    }

    public Task<FriendOperationResult> AcceptAsync(Guid userId, Guid relationId, CancellationToken cancellationToken) =>
        ChangeAsync(userId, relationId, "accept", cancellationToken);

    public Task<FriendOperationResult> RejectAsync(Guid userId, Guid relationId, CancellationToken cancellationToken) =>
        ChangeAsync(userId, relationId, "reject", cancellationToken);

    public Task<FriendOperationResult> CancelAsync(Guid userId, Guid relationId, CancellationToken cancellationToken) =>
        ChangeAsync(userId, relationId, "cancel", cancellationToken);

    public Task<FriendOperationResult> RemoveAsync(Guid userId, Guid relationId, CancellationToken cancellationToken) =>
        ChangeAsync(userId, relationId, "remove", cancellationToken);

    private async Task<FriendOperationResult> ChangeAsync(
        Guid userId, Guid relationId, string action, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var initial = await dbContext.FriendRelations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == relationId, cancellationToken);
        if (initial is null)
            return FriendOperationResult.Failure("Заявка или дружба не найдена.", "none", "not_found");

        var users = await LockUsersAsync(initial.UserLowId, initial.UserHighId, cancellationToken);
        if (users.Count != 2 || users.Any(user => user.IsBlocked))
            return FriendOperationResult.Failure("Действие недоступно.", "unavailable", "player_unavailable");

        var relation = await dbContext.FriendRelations
            .FromSqlInterpolated($"SELECT * FROM friend_relations WHERE \"Id\" = {relationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (relation is null)
            return FriendOperationResult.Failure("Заявка или дружба не найдена.", "none", "not_found");

        if (action == "accept")
        {
            if (relation.Status != "Pending" || relation.RecipientId != userId)
                return FriendOperationResult.Failure("Принять заявку может только получатель.", "none", "forbidden");
            relation.Status = "Accepted";
            relation.UpdatedAtUtc = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return FriendOperationResult.Success("Заявка принята.", "friends", relation.Id);
        }

        if (action == "reject")
        {
            if (relation.Status != "Pending" || relation.RecipientId != userId)
                return FriendOperationResult.Failure("Отклонить заявку может только получатель.", "none", "forbidden");
            dbContext.FriendRelations.Remove(relation);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return FriendOperationResult.Success("Заявка отклонена.", "none");
        }

        if (action == "cancel")
        {
            if (relation.Status != "Pending" || relation.SenderId != userId)
                return FriendOperationResult.Failure("Отменить заявку может только отправитель.", "none", "forbidden");
            dbContext.FriendRelations.Remove(relation);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return FriendOperationResult.Success("Заявка отменена.", "none");
        }

        if (relation.Status != "Accepted" ||
            (relation.UserLowId != userId && relation.UserHighId != userId))
            return FriendOperationResult.Failure("Удалить дружбу может только её участник.", "none", "forbidden");
        dbContext.FriendRelations.Remove(relation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendOperationResult.Success("Игрок удалён из друзей.", "none");
    }

    private async Task<List<User>> LockUsersAsync(Guid first, Guid second, CancellationToken cancellationToken)
    {
        var (low, high) = FriendRules.CanonicalPair(first, second);
        var result = new List<User>(2);
        var lowUser = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE \"Id\" = {low} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (lowUser is not null) result.Add(lowUser);
        var highUser = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE \"Id\" = {high} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (highUser is not null) result.Add(highUser);
        return result;
    }
}
