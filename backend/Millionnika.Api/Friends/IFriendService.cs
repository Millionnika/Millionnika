namespace Millionnika.Api.Friends;

public interface IFriendService
{
    Task<FriendStatusResponse> GetStatusAsync(Guid userId, Guid playerId, CancellationToken cancellationToken);
    Task<FriendListResponse> GetListAsync(Guid userId, string list, int page, int pageSize, CancellationToken cancellationToken);
    Task<FriendOperationResult> SendRequestAsync(Guid userId, Guid playerId, CancellationToken cancellationToken);
    Task<FriendOperationResult> AcceptAsync(Guid userId, Guid relationId, CancellationToken cancellationToken);
    Task<FriendOperationResult> RejectAsync(Guid userId, Guid relationId, CancellationToken cancellationToken);
    Task<FriendOperationResult> CancelAsync(Guid userId, Guid relationId, CancellationToken cancellationToken);
    Task<FriendOperationResult> RemoveAsync(Guid userId, Guid relationId, CancellationToken cancellationToken);
}
