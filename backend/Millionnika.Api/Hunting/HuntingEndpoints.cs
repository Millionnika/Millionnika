using System.Security.Claims;

namespace Millionnika.Api.Hunting;

public static class HuntingEndpoints
{
    public static IEndpointRouteBuilder MapHuntingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/game/hunting").RequireAuthorization();
        group.MapGet("/locations", GetLocationsAsync);
        group.MapGet("/fight", GetFightAsync);
        group.MapPost("/start", StartAsync);
        group.MapPost("/turn", TurnAsync);
        return endpoints;
    }

    private static async Task<IResult> GetLocationsAsync(ClaimsPrincipal principal, IHuntingService service,
        CancellationToken token) => Results.Ok(await service.GetCatalogAsync(UserId(principal), token));

    private static async Task<IResult> GetFightAsync(ClaimsPrincipal principal, IHuntingService service,
        CancellationToken token)
    {
        var fight = await service.GetCurrentFightAsync(UserId(principal), token);
        return fight is null ? Results.NoContent() : Results.Ok(fight);
    }

    private static async Task<IResult> StartAsync(StartHuntRequest request, ClaimsPrincipal principal,
        IHuntingService service, CancellationToken token)
    {
        if (request.RequestId == Guid.Empty || request.TargetId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["hunt"] = ["Выберите цель и передайте requestId."] });
        var result = await service.StartAsync(UserId(principal), request, token);
        return Results.Json(result.Response, statusCode: result.StatusCode);
    }

    private static async Task<IResult> TurnAsync(HuntTurnRequest request, ClaimsPrincipal principal,
        IHuntingService service, CancellationToken token)
    {
        if (request.RequestId == Guid.Empty || request.Action is not ("attack" or "reload") ||
            request.Slot is not ("primary" or "secondary" or "melee"))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["turn"] = ["Некорректное действие боя."] });
        var result = await service.TurnAsync(UserId(principal), request, token);
        return Results.Json(result.Response, statusCode: result.StatusCode);
    }

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
