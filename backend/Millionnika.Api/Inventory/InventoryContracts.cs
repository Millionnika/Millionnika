namespace Millionnika.Api.Inventory;

public sealed record PurchaseRequest(Guid RequestId, Guid ItemId, int Quantity);
public sealed record UseItemRequest(Guid RequestId, Guid ItemId, int Quantity);
public sealed record EquipItemRequest(Guid RequestId, Guid InstanceId, string? Slot);
public sealed record UnequipItemRequest(Guid RequestId, string? Slot);

public sealed record ShopItemResponse(
    Guid Id,
    string Name,
    string Description,
    long Price,
    string Type,
    string EffectResource,
    int EffectAmount,
    string AllowedSlots,
    decimal MinimumDamage,
    decimal MaximumDamage,
    decimal MinimumAccuracy,
    decimal MaximumAccuracy,
    decimal MinimumArmor,
    decimal MaximumArmor,
    string ArmorCoverage,
    string Caliber, int ClipSize, int RateOfFireMin, int RateOfFireMax);

public sealed record InventoryItemResponse(
    Guid ItemId,
    string Name,
    string Description,
    string Type,
    string EffectResource,
    int EffectAmount,
    int Quantity,
    Guid? InstanceId,
    decimal Damage,
    decimal Accuracy,
    decimal Armor,
    string? EquippedSlot,
    string AllowedSlots,
    string ArmorCoverage,
    string Caliber, int ClipSize, int RateOfFireMin, int RateOfFireMax);

public sealed record InventoryResourcesResponse(
    int Energy,
    int MaxEnergy,
    int Nerve,
    int MaxNerve,
    int Health,
    int MaxHealth);

public sealed record InventoryActionResponse(
    Guid RequestId,
    bool Replayed,
    bool Succeeded,
    string Message,
    long Credits,
    int InventoryQuantity,
    InventoryResourcesResponse Resources);

public sealed record InventoryActionResult(InventoryActionResponse Response, bool Rejected);
