using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;

namespace Millionnika.Api.Inventory;

public sealed class InventoryService(
    GameDbContext dbContext,
    IResourceRecoveryService resourceRecovery) : IInventoryService
{
    private const string PurchaseAction = "shop_purchase";
    private const string UseAction = "item_use";

    public async Task<IReadOnlyList<ShopItemResponse>> GetShopAsync(CancellationToken cancellationToken) =>
        await dbContext.Items.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Price)
            .Select(item => new ShopItemResponse(
                item.Id, item.Name, item.Description, item.Price, item.Type,
                item.EffectResource, item.EffectAmount))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InventoryItemResponse>> GetInventoryAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.InventoryItems.AsNoTracking()
            .Where(entry => entry.Character.UserId == userId && entry.Quantity > 0)
            .OrderBy(entry => entry.Item.Name)
            .Select(entry => new InventoryItemResponse(
                entry.ItemId, entry.Item.Name, entry.Item.Description, entry.Item.Type,
                entry.Item.EffectResource, entry.Item.EffectAmount, entry.Quantity))
            .ToListAsync(cancellationToken);

    public async Task<InventoryActionResult> BuyAsync(
        Guid userId,
        Guid requestId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var character = await LockCharacterAsync(userId, cancellationToken);
        var existing = await FindExistingAsync(userId, requestId, cancellationToken);
        if (existing is not null)
        {
            var replay = await ReplayAsync(existing, PurchaseAction, character, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var item = await dbContext.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId && candidate.IsActive,
            cancellationToken);
        ActionLog log;

        if (item is null)
        {
            log = CreateLog(userId, requestId, PurchaseAction, false,
                "Предмет недоступен в магазине.", "item_unavailable", itemId, quantity);
        }
        else if (!ShopRules.TryCalculatePrice(item.Price, quantity, out var totalPrice))
        {
            log = CreateLog(userId, requestId, PurchaseAction, false,
                "Некорректные параметры покупки.", "invalid_purchase", itemId, quantity);
        }
        else if (!ShopRules.HasEnoughCredits(character.Credits, totalPrice))
        {
            log = CreateLog(userId, requestId, PurchaseAction, false,
                "Недостаточно кредитов.", "insufficient_credits", itemId, quantity);
        }
        else
        {
            var inventory = await dbContext.InventoryItems.SingleOrDefaultAsync(
                entry => entry.CharacterId == character.Id && entry.ItemId == itemId,
                cancellationToken);

            if (inventory is not null && inventory.Quantity > int.MaxValue - quantity)
            {
                log = CreateLog(userId, requestId, PurchaseAction, false,
                    "Достигнут предел количества предмета.", "inventory_limit", itemId, quantity);
            }
            else
            {
                character.Credits -= totalPrice;
                if (inventory is null)
                {
                    dbContext.InventoryItems.Add(new PlayerInventoryItem
                    {
                        Id = Guid.NewGuid(),
                        CharacterId = character.Id,
                        ItemId = itemId,
                        Quantity = quantity
                    });
                }
                else
                {
                    inventory.Quantity += quantity;
                }

                log = CreateLog(userId, requestId, PurchaseAction, true,
                    $"Куплено: {item.Name} ×{quantity}.", null, itemId, quantity, totalPrice);
            }
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
        resourceRecovery.Apply(character);
        var existing = await FindExistingAsync(userId, requestId, cancellationToken);
        if (existing is not null)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var replay = await ReplayAsync(existing, UseAction, character, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var item = await dbContext.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId,
            cancellationToken);
        var inventory = await dbContext.InventoryItems.SingleOrDefaultAsync(
            entry => entry.CharacterId == character.Id && entry.ItemId == itemId,
            cancellationToken);
        ActionLog log;

        if (item is null || item.Type != "consumable")
        {
            log = CreateLog(userId, requestId, UseAction, false,
                "Этот предмет нельзя использовать.", "item_unavailable", itemId, quantity);
        }
        else if (inventory is null || !ShopRules.HasEnoughItems(inventory.Quantity, quantity))
        {
            log = CreateLog(userId, requestId, UseAction, false,
                "Недостаточно предметов в инвентаре.", "insufficient_items", itemId, quantity);
        }
        else if (!TryApplyEffect(character, item, quantity, out var resourceWasFull))
        {
            log = CreateLog(userId, requestId, UseAction, false,
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

            log = CreateLog(userId, requestId, UseAction, true,
                $"Использовано: {item.Name} ×{quantity}.", null, itemId, quantity);
        }

        dbContext.ActionLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        var result = await BuildResultAsync(log, character, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<InventoryActionResult> ReplayAsync(
        ActionLog log,
        string expectedAction,
        GameCharacter character,
        CancellationToken cancellationToken)
    {
        if (log.ActionType != expectedAction)
        {
            var response = CreateResponse(log.RequestId, false,
                "Этот ключ действия уже использован.", character, 0);
            return new InventoryActionResult(response, true);
        }

        return await BuildResultAsync(log, character, cancellationToken);
    }

    private async Task<InventoryActionResult> BuildResultAsync(
        ActionLog log,
        GameCharacter character,
        CancellationToken cancellationToken)
    {
        var inventoryQuantity = log.ItemId is null
            ? 0
            : await dbContext.InventoryItems.AsNoTracking()
                .Where(entry => entry.CharacterId == character.Id && entry.ItemId == log.ItemId)
                .Select(entry => entry.Quantity)
                .SingleOrDefaultAsync(cancellationToken);

        return new InventoryActionResult(
            CreateResponse(log.RequestId, log.Succeeded, log.Message, character, inventoryQuantity),
            !log.Succeeded);
    }

    private static InventoryActionResponse CreateResponse(
        Guid requestId,
        bool succeeded,
        string message,
        GameCharacter character,
        int inventoryQuantity) =>
        new(
            requestId,
            succeeded,
            message,
            character.Credits,
            inventoryQuantity,
            new InventoryResourcesResponse(
                character.Energy, character.MaxEnergy,
                character.Nerve, character.MaxNerve,
                character.Health, character.MaxHealth));

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

    private ActionLog CreateLog(
        Guid userId,
        Guid requestId,
        string actionType,
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
            Succeeded = succeeded,
            ErrorCode = errorCode,
            Message = message,
            ItemId = itemId,
            ItemQuantity = itemQuantity,
            CreditsSpent = creditsSpent,
            CreatedAtUtc = resourceRecovery.UtcNow
        };
}
