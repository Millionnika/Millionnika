namespace Millionnika.Api.Inventory;

public interface IInventoryService
{
    Task<IReadOnlyList<ShopItemResponse>> GetShopAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryItemResponse>> GetInventoryAsync(Guid userId, CancellationToken cancellationToken);
    Task<InventoryActionResult> BuyAsync(Guid userId, Guid requestId, Guid itemId, int quantity, CancellationToken cancellationToken);
    Task<InventoryActionResult> UseAsync(Guid userId, Guid requestId, Guid itemId, int quantity, CancellationToken cancellationToken);
    Task<InventoryActionResult> EquipAsync(Guid userId, Guid requestId, Guid instanceId, string slot, CancellationToken cancellationToken);
    Task<InventoryActionResult> UnequipAsync(Guid userId, Guid requestId, string slot, CancellationToken cancellationToken);
}
