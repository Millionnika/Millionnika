using System.Security.Claims;

namespace Millionnika.Api.Players;

public static class PlayerDirectoryEndpoints
{
    public static IEndpointRouteBuilder MapPlayerDirectoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var players = endpoints.MapGroup("/api/players").RequireAuthorization();
        players.MapGet("", SearchAsync);
        players.MapGet("/{playerId:guid}", GetProfileAsync);
        players.MapPut("/{playerId:guid}/description", UpdateDescriptionAsync);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        string? query,
        int? page,
        int? pageSize,
        IPlayerDirectoryService service,
        CancellationToken cancellationToken)
    {
        var requestedPage = page ?? 1;
        var requestedPageSize = pageSize ?? PlayerDirectoryRules.DefaultPageSize;
        var errors = new Dictionary<string, string[]>();
        if (!PlayerDirectoryRules.IsSearchValid(query))
            errors["query"] = [$"Поиск ограничен {PlayerDirectoryRules.MaxSearchLength} символами."];
        if (!PlayerDirectoryRules.IsPageValid(requestedPage, requestedPageSize))
            errors["page"] = [$"Страница должна быть положительной, размер — от 1 до {PlayerDirectoryRules.MaxPageSize}."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        return Results.Ok(await service.SearchAsync(query, requestedPage, requestedPageSize, cancellationToken));
    }

    private static async Task<IResult> GetProfileAsync(
        Guid playerId,
        ClaimsPrincipal principal,
        IPlayerDirectoryService service,
        CancellationToken cancellationToken)
    {
        var profile = await service.GetProfileAsync(GetUserId(principal), playerId, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    private static async Task<IResult> UpdateDescriptionAsync(
        Guid playerId,
        UpdateDescriptionRequest request,
        ClaimsPrincipal principal,
        IPlayerDirectoryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateDescriptionAsync(
            GetUserId(principal), playerId, request.Description, cancellationToken);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
