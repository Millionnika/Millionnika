using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;

namespace Millionnika.Api.Players;

public sealed class PlayerDirectoryService(GameDbContext dbContext) : IPlayerDirectoryService
{
    public async Task<PlayerSearchResponse> SearchAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim().ToUpperInvariant() ?? string.Empty;
        var players = dbContext.Users.AsNoTracking().Where(user => !user.IsBlocked);
        if (normalizedQuery.Length > 0)
        {
            players = players.Where(user => user.NormalizedUsername.Contains(normalizedQuery));
        }

        var totalCount = await players.CountAsync(cancellationToken);
        var result = await players
            .OrderBy(user => user.NormalizedUsername)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new PublicPlayerSummary(
                user.Id, user.Username, user.Character.Level, user.Description))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PlayerSearchResponse(result, page, pageSize, totalCount, totalPages);
    }

    public Task<PublicPlayerProfile?> GetProfileAsync(
        Guid viewerId,
        Guid playerId,
        CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.Id == playerId && !user.IsBlocked)
            .Select(user => new PublicPlayerProfile(
                user.Id,
                user.Username,
                user.Character.Level,
                user.CreatedAtUtc,
                user.Description,
                user.Id == viewerId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<DescriptionUpdateResult> UpdateDescriptionAsync(
        Guid userId,
        Guid playerId,
        string? description,
        CancellationToken cancellationToken)
    {
        if (userId != playerId)
            return DescriptionUpdateResult.Failure("Можно редактировать только своё описание.");
        if (!PlayerDirectoryRules.IsDescriptionValid(description))
            return DescriptionUpdateResult.Failure("Описание должно быть обычным текстом длиной до 300 символов.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.IsBlocked)
            return DescriptionUpdateResult.Failure("Профиль недоступен.");

        user.Description = description!.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DescriptionUpdateResult.Success(user.Description);
    }
}
