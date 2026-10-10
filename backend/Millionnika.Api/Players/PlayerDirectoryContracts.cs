namespace Millionnika.Api.Players;

public sealed record PublicPlayerSummary(Guid Id, string Username, int Level, string Description);

public sealed record PlayerSearchResponse(
    IReadOnlyList<PublicPlayerSummary> Players,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record PublicPlayerProfile(
    Guid Id,
    string Username,
    int Level,
    DateTimeOffset RegisteredAtUtc,
    string Description,
    bool CanEdit);

public sealed record UpdateDescriptionRequest(string? Description);

public sealed record DescriptionUpdateResult(bool Succeeded, string Message, string? Description = null)
{
    public static DescriptionUpdateResult Success(string description) =>
        new(true, "Описание сохранено.", description);

    public static DescriptionUpdateResult Failure(string message) => new(false, message);
}
