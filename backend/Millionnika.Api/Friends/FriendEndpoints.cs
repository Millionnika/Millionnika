using System.Security.Claims;

namespace Millionnika.Api.Friends;

public static class FriendEndpoints
{
    public static IEndpointRouteBuilder MapFriendEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var friends = endpoints.MapGroup("/api/friends").RequireAuthorization();
        friends.MapGet("", GetListAsync);
        friends.MapGet("/status/{playerId:guid}", GetStatusAsync);
        friends.MapPost("/requests", SendAsync);
        friends.MapDelete("/requests/{relationId:guid}", CancelAsync);
        friends.MapPost("/requests/{relationId:guid}/accept", AcceptAsync);
        friends.MapPost("/requests/{relationId:guid}/reject", RejectAsync);
        friends.MapDelete("/{relationId:guid}", RemoveAsync);
        return endpoints;
    }

    private static async Task<IResult> GetListAsync(string? list, int? page, int? pageSize,
        ClaimsPrincipal principal, IFriendService service, CancellationToken cancellationToken)
    {
        var requestedList = list ?? "friends";
        var requestedPage = page ?? 1;
        var requestedPageSize = pageSize ?? FriendRules.DefaultPageSize;
        if (!FriendRules.IsListValid(requestedList) || !FriendRules.IsPageValid(requestedPage, requestedPageSize))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["page"] = ["Недопустимый список или размер страницы."] });
        return Results.Ok(await service.GetListAsync(
            GetUserId(principal), requestedList, requestedPage, requestedPageSize, cancellationToken));
    }

    private static async Task<IResult> GetStatusAsync(Guid playerId, ClaimsPrincipal principal,
        IFriendService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.GetStatusAsync(GetUserId(principal), playerId, cancellationToken));

    private static async Task<IResult> SendAsync(SendFriendRequest request, ClaimsPrincipal principal,
        IFriendService service, CancellationToken cancellationToken) =>
        Map(await service.SendRequestAsync(GetUserId(principal), request.PlayerId, cancellationToken));

    private static async Task<IResult> AcceptAsync(Guid relationId, ClaimsPrincipal principal,
        IFriendService service, CancellationToken cancellationToken) =>
        Map(await service.AcceptAsync(GetUserId(principal), relationId, cancellationToken));

    private static async Task<IResult> RejectAsync(Guid relationId, ClaimsPrincipal principal,
        IFriendService service, CancellationToken cancellationToken) =>
        Map(await service.RejectAsync(GetUserId(principal), relationId, cancellationToken));

    private static async Task<IResult> CancelAsync(Guid relationId, ClaimsPrincipal principal,
        IFriendService service, CancellationToken cancellationToken) =>
        Map(await service.CancelAsync(GetUserId(principal), relationId, cancellationToken));

    private static async Task<IResult> RemoveAsync(Guid relationId, ClaimsPrincipal principal,
        IFriendService service, CancellationToken cancellationToken) =>
        Map(await service.RemoveAsync(GetUserId(principal), relationId, cancellationToken));

    private static IResult Map(FriendOperationResult result) => result.Succeeded
        ? Results.Ok(result)
        : result.ErrorCode switch
        {
            "forbidden" => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
            "not_found" => Results.NotFound(result),
            "self_request" => Results.BadRequest(result),
            "player_unavailable" => Results.NotFound(result),
            _ => Results.Conflict(result)
        };

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
