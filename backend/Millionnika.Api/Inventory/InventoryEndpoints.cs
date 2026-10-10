using System.Security.Claims;

namespace Millionnika.Api.Inventory;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var game = endpoints.MapGroup("/api/game").RequireAuthorization();
        game.MapGet("/shop", GetShopAsync);
        game.MapPost("/shop/buy", BuyAsync);
        game.MapGet("/inventory", GetInventoryAsync);
        game.MapPost("/inventory/use", UseAsync);
        return endpoints;
    }

    private static async Task<IResult> GetShopAsync(
        IInventoryService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetShopAsync(cancellationToken));

    private static async Task<IResult> GetInventoryAsync(
        ClaimsPrincipal principal,
        IInventoryService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetInventoryAsync(GetUserId(principal), cancellationToken));

    private static async Task<IResult> BuyAsync(
        PurchaseRequest request,
        ClaimsPrincipal principal,
        IInventoryService service,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request.RequestId, request.ItemId, request.Quantity);
        if (validation is not null) return validation;

        var result = await service.BuyAsync(
            GetUserId(principal), request.RequestId, request.ItemId, request.Quantity, cancellationToken);
        return result.Rejected ? Results.Conflict(result.Response) : Results.Ok(result.Response);
    }

    private static async Task<IResult> UseAsync(
        UseItemRequest request,
        ClaimsPrincipal principal,
        IInventoryService service,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request.RequestId, request.ItemId, request.Quantity);
        if (validation is not null) return validation;

        var result = await service.UseAsync(
            GetUserId(principal), request.RequestId, request.ItemId, request.Quantity, cancellationToken);
        return result.Rejected ? Results.Conflict(result.Response) : Results.Ok(result.Response);
    }

    private static IResult? Validate(Guid requestId, Guid itemId, int quantity)
    {
        var errors = new Dictionary<string, string[]>();
        if (requestId == Guid.Empty) errors["requestId"] = ["Некорректный ключ действия."];
        if (itemId == Guid.Empty) errors["itemId"] = ["Предмет не выбран."];
        if (!ShopRules.IsValidQuantity(quantity))
            errors["quantity"] = [$"Количество должно быть от 1 до {ShopRules.MaxQuantityPerRequest}."];
        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
