using System.Security.Claims;

namespace Millionnika.Api.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        admin.MapGet("/players", GetPlayersAsync);
        admin.MapGet("/players/{playerId:guid}", GetPlayerAsync);
        admin.MapPut("/players/{playerId:guid}/block", SetBlockAsync);
        admin.MapPut("/players/{playerId:guid}/resources", UpdateResourcesAsync);
        admin.MapGet("/contracts", GetContractsAsync);
        admin.MapPut("/contracts/{contractId:guid}", UpdateContractAsync);
        admin.MapGet("/items", GetItemsAsync);
        admin.MapPut("/items/{itemId:guid}", UpdateItemAsync);
        admin.MapGet("/action-logs", GetActionLogsAsync);
        admin.MapGet("/audit-logs", GetAuditLogsAsync);
        return endpoints;
    }

    private static async Task<IResult> GetPlayersAsync(IAdminService service, CancellationToken token) =>
        Results.Ok(await service.GetPlayersAsync(token));

    private static async Task<IResult> GetPlayerAsync(Guid playerId, IAdminService service, CancellationToken token)
    {
        var player = await service.GetPlayerAsync(playerId, token);
        return player is null ? Results.NotFound() : Results.Ok(player);
    }

    private static async Task<IResult> SetBlockAsync(
        Guid playerId, SetBlockRequest request, ClaimsPrincipal principal,
        IAdminService service, CancellationToken token) =>
        ToResult(await service.SetBlockAsync(GetUserId(principal), playerId, request, token));

    private static async Task<IResult> UpdateResourcesAsync(
        Guid playerId, UpdateResourcesRequest request, ClaimsPrincipal principal,
        IAdminService service, CancellationToken token) =>
        ToResult(await service.UpdateResourcesAsync(GetUserId(principal), playerId, request, token));

    private static async Task<IResult> GetContractsAsync(IAdminService service, CancellationToken token) =>
        Results.Ok(await service.GetContractsAsync(token));

    private static async Task<IResult> UpdateContractAsync(
        Guid contractId, UpdateContractRequest request, ClaimsPrincipal principal,
        IAdminService service, CancellationToken token) =>
        ToResult(await service.UpdateContractAsync(GetUserId(principal), contractId, request, token));

    private static async Task<IResult> GetItemsAsync(IAdminService service, CancellationToken token) =>
        Results.Ok(await service.GetItemsAsync(token));

    private static async Task<IResult> UpdateItemAsync(
        Guid itemId, UpdateItemRequest request, ClaimsPrincipal principal,
        IAdminService service, CancellationToken token) =>
        ToResult(await service.UpdateItemAsync(GetUserId(principal), itemId, request, token));

    private static async Task<IResult> GetActionLogsAsync(IAdminService service, CancellationToken token) =>
        Results.Ok(await service.GetActionLogsAsync(token));

    private static async Task<IResult> GetAuditLogsAsync(IAdminService service, CancellationToken token) =>
        Results.Ok(await service.GetAuditLogsAsync(token));

    private static IResult ToResult(AdminMutationResult result) =>
        result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
