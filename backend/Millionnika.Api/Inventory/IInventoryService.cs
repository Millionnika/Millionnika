namespace Millionnika.Api.Inventory;

public interface IInventoryService
{
    Task<IReadOnlyList<ShopItemResponse>> GetShopAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryItemResponse>> GetInventoryAsync(Guid userId, CancellationToken cancellationToken);
    Task<InventoryActionResult> BuyAsync(Guid userId, Guid requestId, Guid itemId, int quantity, CancellationToken cancellationToken);
    Task<InventoryActionResult> UseAsync(Guid userId, Guid requestId, Guid itemId, int quantity, CancellationToken cancellationToken);
}
