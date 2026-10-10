namespace Millionnika.Api.Friends;

public sealed record SendFriendRequest(Guid PlayerId);

public sealed record FriendStatusResponse(string State, Guid? RelationId);

public sealed record FriendListItem(
    Guid RelationId,
    Guid PlayerId,
    string Username,
    int Level,
    DateTimeOffset CreatedAtUtc);

public sealed record FriendListResponse(
    IReadOnlyList<FriendListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record FriendOperationResult(
    bool Succeeded,
    string Message,
    string State,
    Guid? RelationId = null,
    string? ErrorCode = null)
{
    public static FriendOperationResult Success(string message, string state, Guid? relationId = null) =>
        new(true, message, state, relationId);

    public static FriendOperationResult Failure(string message, string state, string errorCode, Guid? relationId = null) =>
        new(false, message, state, relationId, errorCode);
}
