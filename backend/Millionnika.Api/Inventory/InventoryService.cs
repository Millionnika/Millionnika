using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Resources;

namespace Millionnika.Api.Inventory;

public sealed class InventoryService(
    GameDbContext dbContext,
    IResourceRecoveryService resourceRecovery,
    IEquipmentRoller equipmentRoller) : IInventoryService
{
    private const string PurchaseAction = "shop_purchase";
    private const string UseAction = "item_use";
    private const string EquipAction = "item_equip";
    private const string UnequipAction = "item_unequip";

    public async Task<IReadOnlyList<ShopItemResponse>> GetShopAsync(CancellationToken cancellationToken) =>
        await dbContext.Items.AsNoTracking()
            .Where(item => item.IsActive && item.Type != ItemTypes.Material)
            .OrderBy(item => item.Price)
            .Select(item => new ShopItemResponse(
                item.Id, item.Name, item.Description, item.Price, item.Type,
                item.EffectResource, item.EffectAmount, item.AllowedSlots,
                item.MinimumDamage, item.MaximumDamage, item.MinimumAccuracy, item.MaximumAccuracy,
                item.MinimumArmor, item.MaximumArmor, item.ArmorCoverage,
                item.Caliber, item.ClipSize, item.RateOfFireMin, item.RateOfFireMax,
                item.CooldownSeconds, item.CooldownGroup))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InventoryItemResponse>> GetInventoryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var consumables = await dbContext.InventoryItems.AsNoTracking()
            .Where(entry => entry.Character.UserId == userId && entry.Quantity > 0)
            .OrderBy(entry => entry.Item.Name)
            .Select(entry => new InventoryItemResponse(
                entry.ItemId, entry.Item.Name, entry.Item.Description, entry.Item.Type,
                entry.Item.EffectResource, entry.Item.EffectAmount, entry.Quantity, null,
                0, 0, 0, null, entry.Item.AllowedSlots, entry.Item.ArmorCoverage,
                entry.Item.Caliber, entry.Item.ClipSize, entry.Item.RateOfFireMin, entry.Item.RateOfFireMax,
                entry.Item.CooldownSeconds, entry.Item.CooldownGroup, null))
            .ToListAsync(cancellationToken);
        var equipment = await dbContext.EquipmentInstances.AsNoTracking()
            .Where(instance => instance.Character.UserId == userId)
            .OrderBy(instance => instance.Item.Name).ThenBy(instance => instance.Id)
            .Select(instance => new InventoryItemResponse(
                instance.ItemId, instance.Item.Name, instance.Item.Description, instance.Item.Type,
                instance.Item.EffectResource, instance.Item.EffectAmount, 1, instance.Id,
                instance.Damage, instance.Accuracy, instance.Armor, instance.EquippedSlot,
                instance.Item.AllowedSlots, instance.Item.ArmorCoverage,
                instance.Item.Caliber, instance.Item.ClipSize, instance.Item.RateOfFireMin, instance.Item.RateOfFireMax,
                instance.Item.CooldownSeconds, instance.Item.CooldownGroup, null))
            .ToListAsync(cancellationToken);
        var cooldowns = await dbContext.ConsumableCooldowns.AsNoTracking()
            .Where(value => dbContext.Characters.Any(character => character.Id == value.CharacterId && character.UserId == userId))
            .ToDictionaryAsync(value => value.Group, value => value.AvailableAtUtc, cancellationToken);
        return consumables.Select(item => item with
        {
            CooldownAvailableAtUtc = item.CooldownSeconds > 0 && cooldowns.TryGetValue(item.CooldownGroup, out var until) && until > resourceRecovery.UtcNow ? until : null
        }).Concat(equipment).ToList();
    }

    public async Task<InventoryActionResult> BuyAsync(
        Guid userId,
        Guid requestId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var character = await LockCharacterAsync(userId, cancellationToken);
        var signature = ActionRequestSignature.Purchase(itemId, quantity);
        var existing = await FindExistingAsync(userId, requestId, cancellationToken);
        if (existing is not null)
        {
            var replay = await ReplayAsync(existing, PurchaseAction, signature, character, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }
        if (await HuntingRequestExistsAsync(userId, requestId, cancellationToken))
            return RequestConflict(requestId, character);

        var item = await dbContext.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId && candidate.IsActive && candidate.Type != ItemTypes.Material,
            cancellationToken);
        ActionLog log;

        if (item is null)
        {
            log = CreateLog(userId, requestId, PurchaseAction, signature, false,
                "Предмет недоступен в магазине.", "item_unavailable", itemId, quantity);
        }
        else if (!ShopRules.TryCalculatePrice(item.Price, quantity, out var totalPrice))
        {
            log = CreateLog(userId, requestId, PurchaseAction, signature, false,
                "Некорректные параметры покупки.", "invalid_purchase", itemId, quantity);
        }
        else if (!ShopRules.HasEnoughCredits(character.Credits, totalPrice))
        {
            log = CreateLog(userId, requestId, PurchaseAction, signature, false,
                "Недостаточно кредитов.", "insufficient_credits", itemId, quantity);
        }
        else
        {
            if (item.Type is ItemTypes.Consumable or ItemTypes.Ammunition)
            {
                var inventory = await dbContext.InventoryItems.SingleOrDefaultAsync(
                    entry => entry.CharacterId == character.Id && entry.ItemId == itemId, cancellationToken);
                if (inventory is not null && inventory.Quantity > int.MaxValue - quantity)
                {
                    log = CreateLog(userId, requestId, PurchaseAction, signature, false,
                        "Достигнут предел количества предмета.", "inventory_limit", itemId, quantity);
                }
                else
                {
                    character.Credits -= totalPrice;
                    if (inventory is null)
                        dbContext.InventoryItems.Add(new PlayerInventoryItem { Id = Guid.NewGuid(), CharacterId = character.Id, ItemId = itemId, Quantity = quantity });
                    else inventory.Quantity += quantity;
                    log = CreateLog(userId, requestId, PurchaseAction, signature, true,
                        $"Куплено: {item.Name} ×{quantity}.", null, itemId, quantity, totalPrice);
                }
            }
            else if ((item.Type == ItemTypes.Weapon || item.Type == ItemTypes.Armor) && EquipmentRules.IsDefinitionValid(item))
            {
                character.Credits -= totalPrice;
                for (var index = 0; index < quantity; index++)
                {
                    dbContext.EquipmentInstances.Add(new EquipmentInstance
                    {
                        Id = Guid.NewGuid(), CharacterId = character.Id, ItemId = itemId,
                        Damage = item.Type == ItemTypes.Weapon ? equipmentRoller.Roll(item.MinimumDamage, item.MaximumDamage) : 0,
                        Accuracy = item.Type == ItemTypes.Weapon ? equipmentRoller.Roll(item.MinimumAccuracy, item.MaximumAccuracy) : 0,
                        Armor = item.Type == ItemTypes.Armor ? equipmentRoller.Roll(item.MinimumArmor, item.MaximumArmor) : 0,
                        CreatedAtUtc = resourceRecovery.UtcNow
                    });
                }
                log = CreateLog(userId, requestId, PurchaseAction, signature, true,
                    $"Куплено: {item.Name} ×{quantity}.", null, itemId, quantity, totalPrice);
            }
            else log = CreateLog(userId, requestId, PurchaseAction, signature, false,
                "Определение экипировки некорректно.", "invalid_item_definition", itemId, quantity);
        }

        dbContext.ActionLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        var result = await BuildResultAsync(log, character, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<InventoryActionResult> UseAsync(
        Guid userId,
        Guid requestId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var character = await LockCharacterAsync(userId, cancellationToken);
        var signature = ActionRequestSignature.UseItem(itemId, quantity);
        var existing = await FindExistingAsync(userId, requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ActionType != UseAction || existing.RequestSignature != signature)
                return RequestConflict(requestId, character);
            resourceRecovery.Apply(character);
            await dbContext.SaveChangesAsync(cancellationToken);
            var replay = await ReplayAsync(existing, UseAction, signature, character, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }
        if (await HuntingRequestExistsAsync(userId, requestId, cancellationToken))
            return RequestConflict(requestId, character);
        resourceRecovery.Apply(character);

        var item = await dbContext.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId,
            cancellationToken);
        var inventory = await dbContext.InventoryItems.SingleOrDefaultAsync(
            entry => entry.CharacterId == character.Id && entry.ItemId == itemId,
            cancellationToken);
        var cooldownRemaining = item is { CooldownSeconds: > 0 }
            ? await GetCooldownRemainingAsync(character.Id, item.CooldownGroup, cancellationToken) : 0;
        ActionLog log;

        if (await HasActiveHuntAsync(character.Id, cancellationToken))
        {
            log = CreateLog(userId, requestId, UseAction, signature, false,
                "Во время охоты расходники недоступны.", "active_hunt", itemId, quantity);
        }
        else if (item is null || item.Type != "consumable")
        {
            log = CreateLog(userId, requestId, UseAction, signature, false,
                "Этот предмет нельзя использовать.", "item_unavailable", itemId, quantity);
        }
        else if (item.CooldownSeconds > 0 && quantity != 1)
        {
            log = CreateLog(userId, requestId, UseAction, signature, false,
                "Предмет с ожиданием можно использовать только по одной штуке.", "cooldown_single_item", itemId, quantity);
        }
        else if (inventory is null || !ShopRules.HasEnoughItems(inventory.Quantity, quantity))
        {
            log = CreateLog(userId, requestId, UseAction, signature, false,
                "Недостаточно предметов в инвентаре.", "insufficient_items", itemId, quantity);
        }
        else if (cooldownRemaining > 0)
        {
            log = CreateLog(userId, requestId, UseAction, signature, false,
                $"Предмет пока недоступен. Осталось {cooldownRemaining} с.", "item_cooldown", itemId, quantity);
        }
        else if (!TryApplyEffect(character, item, quantity, out var resourceWasFull))
        {
            log = CreateLog(userId, requestId, UseAction, signature, false,
                resourceWasFull ? "Ресурс уже полностью восстановлен." : "Эффект предмета не поддерживается.",
                resourceWasFull ? "resource_full" : "invalid_item_effect", itemId, quantity);
        }
        else
        {
            inventory.Quantity -= quantity;
            if (inventory.Quantity == 0)
            {
                dbContext.InventoryItems.Remove(inventory);
            }

            if (item.CooldownSeconds > 0)
            {
                var cooldown = await dbContext.ConsumableCooldowns.SingleOrDefaultAsync(
                    value => value.CharacterId == character.Id && value.Group == item.CooldownGroup, cancellationToken);
                if (cooldown is null)
                    dbContext.ConsumableCooldowns.Add(new ConsumableCooldown
                    {
                        CharacterId = character.Id, Group = item.CooldownGroup,
                        AvailableAtUtc = resourceRecovery.UtcNow.AddSeconds(item.CooldownSeconds)
                    });
                else cooldown.AvailableAtUtc = resourceRecovery.UtcNow.AddSeconds(item.CooldownSeconds);
            }

            log = CreateLog(userId, requestId, UseAction, signature, true,
                $"Использовано: {item.Name} ×{quantity}.", null, itemId, quantity);
        }

        dbContext.ActionLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        var result = await BuildResultAsync(log, character, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<InventoryActionResult> EquipAsync(Guid userId, Guid requestId, Guid instanceId,
        string slot, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var character = await LockCharacterAsync(userId, cancellationToken);
        var signature = ActionRequestSignature.Equip(instanceId, slot);
        var existing = await FindExistingAsync(userId, requestId, cancellationToken);
        if (existing is not null)
        {
            var replay = await ReplayAsync(existing, EquipAction, signature, character, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }
        if (await HuntingRequestExistsAsync(userId, requestId, cancellationToken))
            return RequestConflict(requestId, character);

        var instance = await dbContext.EquipmentInstances.Include(value => value.Item)
            .SingleOrDefaultAsync(value => value.Id == instanceId && value.CharacterId == character.Id, cancellationToken);
        ActionLog log;
        if (await HasActiveHuntAsync(character.Id, cancellationToken))
            log = CreateLog(userId, requestId, EquipAction, signature, false, "Во время охоты менять экипировку нельзя.", "active_hunt", instance?.ItemId ?? Guid.Empty, 0);
        else if (instance is null)
            log = CreateLog(userId, requestId, EquipAction, signature, false, "Экземпляр экипировки не найден у персонажа.", "equipment_not_owned", Guid.Empty, 0);
        else if (!EquipmentSlots.IsValid(slot) || slot == EquipmentSlots.Temporary || !EquipmentSlots.Allows(instance.Item, slot))
            log = CreateLog(userId, requestId, EquipAction, signature, false, "Предмет нельзя надеть в выбранный слот.", "invalid_equipment_slot", instance.ItemId, 1);
        else
        {
            await dbContext.EquipmentInstances.Where(value => value.CharacterId == character.Id &&
                value.EquippedSlot == slot && value.Id != instance.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.EquippedSlot, (string?)null), cancellationToken);
            instance.EquippedSlot = slot;
            log = CreateLog(userId, requestId, EquipAction, signature, true,
                $"Экипировано: {instance.Item.Name}.", null, instance.ItemId, 1);
        }
        dbContext.ActionLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        var result = await BuildResultAsync(log, character, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<InventoryActionResult> UnequipAsync(Guid userId, Guid requestId, string slot,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var character = await LockCharacterAsync(userId, cancellationToken);
        var signature = ActionRequestSignature.Unequip(slot);
        var existing = await FindExistingAsync(userId, requestId, cancellationToken);
        if (existing is not null)
        {
            var replay = await ReplayAsync(existing, UnequipAction, signature, character, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }
        if (await HuntingRequestExistsAsync(userId, requestId, cancellationToken))
            return RequestConflict(requestId, character);
        var instance = await dbContext.EquipmentInstances.Include(value => value.Item)
            .SingleOrDefaultAsync(value => value.CharacterId == character.Id && value.EquippedSlot == slot, cancellationToken);
        var itemId = instance?.ItemId ?? Guid.Empty;
        var activeHunt = await HasActiveHuntAsync(character.Id, cancellationToken);
        if (instance is not null && !activeHunt) instance.EquippedSlot = null;
        var log = CreateLog(userId, requestId, UnequipAction, signature, !activeHunt,
            activeHunt ? "Во время охоты менять экипировку нельзя." : instance is null ? "Слот уже пуст." : $"Снято: {instance.Item.Name}.",
            activeHunt ? "active_hunt" : null, itemId, instance is null ? 0 : 1);
        dbContext.ActionLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        var result = await BuildResultAsync(log, character, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<InventoryActionResult> ReplayAsync(
        ActionLog log,
        string expectedAction,
        string expectedSignature,
        GameCharacter character,
        CancellationToken cancellationToken)
    {
        if (log.ActionType != expectedAction || log.RequestSignature != expectedSignature)
            return RequestConflict(log.RequestId, character);

        return await BuildResultAsync(log, character, cancellationToken, replayed: true);
    }

    private async Task<InventoryActionResult> BuildResultAsync(
        ActionLog log,
        GameCharacter character,
        CancellationToken cancellationToken,
        bool replayed = false)
    {
        var inventoryQuantity = log.ItemId is null || log.ItemId == Guid.Empty
            ? 0
            : await dbContext.InventoryItems.AsNoTracking()
                .Where(entry => entry.CharacterId == character.Id && entry.ItemId == log.ItemId)
                .Select(entry => entry.Quantity)
                .SingleOrDefaultAsync(cancellationToken) +
              await dbContext.EquipmentInstances.AsNoTracking()
                .CountAsync(entry => entry.CharacterId == character.Id && entry.ItemId == log.ItemId, cancellationToken);

        var cooldownRemaining = 0;
        if (log.ActionType == UseAction && log.ItemId is { } itemId)
        {
            var group = await dbContext.Items.AsNoTracking().Where(item => item.Id == itemId)
                .Select(item => item.CooldownGroup).SingleOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrEmpty(group))
                cooldownRemaining = await GetCooldownRemainingAsync(character.Id, group, cancellationToken);
        }

        return new InventoryActionResult(
            CreateResponse(log.RequestId, replayed, log.Succeeded, log.Message, character, inventoryQuantity, cooldownRemaining),
            !log.Succeeded);
    }

    private async Task<int> GetCooldownRemainingAsync(Guid characterId, string group, CancellationToken cancellationToken)
    {
        var availableAt = await dbContext.ConsumableCooldowns.AsNoTracking()
            .Where(value => value.CharacterId == characterId && value.Group == group)
            .Select(value => (DateTimeOffset?)value.AvailableAtUtc).SingleOrDefaultAsync(cancellationToken);
        return availableAt is null ? 0 : (int)Math.Max(0, Math.Ceiling((availableAt.Value - resourceRecovery.UtcNow).TotalSeconds));
    }

    private static InventoryActionResponse CreateResponse(
        Guid requestId,
        bool replayed,
        bool succeeded,
        string message,
        GameCharacter character,
        int inventoryQuantity,
        int cooldownRemainingSeconds = 0) =>
        new(
            requestId,
            replayed,
            succeeded,
            message,
            character.Credits,
            inventoryQuantity,
            new InventoryResourcesResponse(
                character.Energy, character.MaxEnergy,
                character.Nerve, character.MaxNerve,
                character.Health, character.MaxHealth), cooldownRemainingSeconds);

    private static bool TryApplyEffect(
        GameCharacter character,
        Item item,
        int quantity,
        out bool resourceWasFull)
    {
        resourceWasFull = false;
        switch (item.EffectResource)
        {
            case "energy":
                resourceWasFull = character.Energy >= character.MaxEnergy;
                if (!resourceWasFull)
                    character.Energy = ShopRules.CalculateRestoredValue(character.Energy, character.MaxEnergy, item.EffectAmount, quantity);
                return !resourceWasFull;
            case "nerve":
                resourceWasFull = character.Nerve >= character.MaxNerve;
                if (!resourceWasFull)
                    character.Nerve = ShopRules.CalculateRestoredValue(character.Nerve, character.MaxNerve, item.EffectAmount, quantity);
                return !resourceWasFull;
            case "health":
                resourceWasFull = character.Health >= character.MaxHealth;
                if (!resourceWasFull)
                    character.Health = ShopRules.CalculateRestoredValue(character.Health, character.MaxHealth, item.EffectAmount, quantity);
                return !resourceWasFull;
            default:
                return false;
        }
    }

    private async Task<GameCharacter> LockCharacterAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Characters
            .FromSqlInterpolated($"SELECT * FROM characters WHERE \"UserId\" = {userId} FOR UPDATE")
            .SingleAsync(cancellationToken);

    private Task<ActionLog?> FindExistingAsync(Guid userId, Guid requestId, CancellationToken cancellationToken) =>
        dbContext.ActionLogs.AsNoTracking().SingleOrDefaultAsync(
            log => log.UserId == userId && log.RequestId == requestId,
            cancellationToken);

    private Task<bool> HasActiveHuntAsync(Guid characterId, CancellationToken cancellationToken) =>
        dbContext.HuntFights.AnyAsync(value => value.CharacterId == characterId && value.Status == "active", cancellationToken);

    private Task<bool> HuntingRequestExistsAsync(Guid userId, Guid requestId, CancellationToken cancellationToken) =>
        dbContext.HuntingRequests.AsNoTracking().AnyAsync(value => value.UserId == userId && value.RequestId == requestId, cancellationToken);

    private ActionLog CreateLog(
        Guid userId,
        Guid requestId,
        string actionType,
        string requestSignature,
        bool succeeded,
        string message,
        string? errorCode,
        Guid itemId,
        int itemQuantity,
        long creditsSpent = 0) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RequestId = requestId,
            ActionType = actionType,
            RequestSignature = requestSignature,
            Succeeded = succeeded,
            ErrorCode = errorCode,
            Message = message,
            ItemId = itemId,
            ItemQuantity = itemQuantity,
            CreditsSpent = creditsSpent,
            CreatedAtUtc = resourceRecovery.UtcNow
        };

    private static InventoryActionResult RequestConflict(Guid requestId, GameCharacter character) => new(
        CreateResponse(requestId, false, false,
            "Этот requestId уже использован для другого действия или других параметров.",
            character, 0),
        Rejected: true);
}
