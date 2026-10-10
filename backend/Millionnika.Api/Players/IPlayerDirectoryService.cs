namespace Millionnika.Api.Players;

public interface IPlayerDirectoryService
{
    Task<PlayerSearchResponse> SearchAsync(string? query, int page, int pageSize, CancellationToken cancellationToken);
    Task<PublicPlayerProfile?> GetProfileAsync(Guid viewerId, Guid playerId, CancellationToken cancellationToken);
    Task<DescriptionUpdateResult> UpdateDescriptionAsync(
        Guid userId, Guid playerId, string? description, CancellationToken cancellationToken);
}
