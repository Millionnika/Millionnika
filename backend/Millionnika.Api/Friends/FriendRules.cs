namespace Millionnika.Api.Friends;

public static class FriendRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 20;
    public const int MaxPageNumber = 100_000;

    public static bool IsListValid(string? list) => list is "friends" or "incoming" or "outgoing";

    public static bool IsPageValid(int page, int pageSize) =>
        page is >= 1 and <= MaxPageNumber && pageSize is >= 1 and <= MaxPageSize;

    public static (Guid Low, Guid High) CanonicalPair(Guid first, Guid second) =>
        first.CompareTo(second) < 0 ? (first, second) : (second, first);
}
