namespace Millionnika.Api.Players;

public static class PlayerDirectoryRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 20;
    public const int MaxSearchLength = 24;
    public const int MaxDescriptionLength = 300;
    public const int MaxPageNumber = 100_000;

    public static bool IsSearchValid(string? query) =>
        (query?.Trim().Length ?? 0) <= MaxSearchLength;

    public static bool IsPageValid(int page, int pageSize) =>
        page is >= 1 and <= MaxPageNumber && pageSize is >= 1 and <= MaxPageSize;

    public static bool IsDescriptionValid(string? description) =>
        description is not null && description.Length <= MaxDescriptionLength &&
        description.All(character => character is '\r' or '\n' or '\t' || !char.IsControl(character));
}
