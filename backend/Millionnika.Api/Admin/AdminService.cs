using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;

namespace Millionnika.Api.Admin;

public sealed class AdminService(
    GameDbContext dbContext,
    IResourceRecoveryService resourceRecovery) : IAdminService
{
    public async Task<IReadOnlyList<AdminPlayerSummary>> GetPlayersAsync(CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking().OrderBy(user => user.Username)
            .Select(user => new AdminPlayerSummary(
                user.Id, user.Username, user.Email, user.Role, user.IsBlocked, user.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public Task<AdminPlayerDetails?> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().Where(user => user.Id == playerId)
            .Select(user => new AdminPlayerDetails(
                user.Id, user.Username, user.Email, user.Role, user.IsBlocked, user.CreatedAtUtc,
                user.Character.Level, user.Character.Credits,
                user.Character.Energy, user.Character.MaxEnergy,
                user.Character.Nerve, user.Character.MaxNerve,
                user.Character.Health, user.Character.MaxHealth,
                user.Character.Strength, user.Character.Speed, user.Character.Defense,
                user.Character.Dexterity, user.Character.Experience))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminMutationResult> SetBlockAsync(
        Guid adminId, Guid playerId, SetBlockRequest request, CancellationToken cancellationToken)
    {
        if (!AdminRules.IsReasonValid(request.Reason)) return InvalidReason();
        if (adminId == playerId) return AdminMutationResult.Failure("Нельзя заблокировать собственную учётную запись.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE \"Id\" = {playerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return AdminMutationResult.Failure("Игрок не найден.");

        var oldValue = new { user.IsBlocked };
        user.IsBlocked = request.IsBlocked;
        if (request.IsBlocked)
            await dbContext.UserSessions.Where(session => session.UserId == playerId).ExecuteDeleteAsync(cancellationToken);
        AddAudit(adminId, "player_block", "user", playerId.ToString(), oldValue,
            new { user.IsBlocked }, request.Reason!);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMutationResult.Success(request.IsBlocked ? "Игрок заблокирован." : "Игрок разблокирован.");
    }

    public async Task<AdminMutationResult> UpdateResourcesAsync(
        Guid adminId, Guid playerId, UpdateResourcesRequest request, CancellationToken cancellationToken)
    {
        if (!AdminRules.IsReasonValid(request.Reason)) return InvalidReason();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var character = await dbContext.Characters
            .FromSqlInterpolated($"SELECT * FROM characters WHERE \"UserId\" = {playerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null) return AdminMutationResult.Failure("Игрок не найден.");
        if (!AdminRules.AreResourcesValid(
                request.Credits, request.Energy, character.MaxEnergy,
                request.Nerve, character.MaxNerve, request.Health, character.MaxHealth))
            return AdminMutationResult.Failure(
                "Ресурсы должны быть от нуля до установленного максимума; кредиты — не более 1 триллиона.");

        var oldValue = Resources(character);
        var now = resourceRecovery.UtcNow;
        character.Credits = request.Credits;
        character.Energy = request.Energy;
        character.Nerve = request.Nerve;
        character.Health = request.Health;
        character.LastEnergyRecoveryAtUtc = now;
        character.LastNerveRecoveryAtUtc = now;
        AddAudit(adminId, "resources_update", "user", playerId.ToString(), oldValue,
            Resources(character), request.Reason!);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMutationResult.Success("Ресурсы обновлены.");
    }

    public async Task<IReadOnlyList<AdminContractResponse>> GetContractsAsync(CancellationToken cancellationToken) =>
        await dbContext.Contracts.AsNoTracking().OrderBy(contract => contract.Name)
            .Select(contract => new AdminContractResponse(
                contract.Id, contract.Name, contract.CostNerve, contract.SuccessChance,
                contract.RewardCredits, contract.RewardExperience, contract.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<AdminMutationResult> UpdateContractAsync(
        Guid adminId, Guid contractId, UpdateContractRequest request, CancellationToken cancellationToken)
    {
        if (!AdminRules.IsContractValid(request))
            return AdminMutationResult.Failure("Проверьте параметры контракта: награды не могут превышать 1 триллион.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var contract = await dbContext.Contracts
            .FromSqlInterpolated($"SELECT * FROM contracts WHERE \"Id\" = {contractId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (contract is null) return AdminMutationResult.Failure("Контракт не найден.");

        var oldValue = ContractValues(contract);
        contract.CostNerve = request.CostNerve;
        contract.SuccessChance = request.SuccessChance;
        contract.RewardCredits = request.RewardCredits;
        contract.RewardExperience = request.RewardExperience;
        contract.IsActive = request.IsActive;
        AddAudit(adminId, "contract_update", "contract", contractId.ToString(), oldValue,
            ContractValues(contract), request.Reason!);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMutationResult.Success("Контракт обновлён.");
    }

    public async Task<IReadOnlyList<AdminItemResponse>> GetItemsAsync(CancellationToken cancellationToken) =>
        await dbContext.Items.AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new AdminItemResponse(
                item.Id, item.Name, item.Description, item.Price, item.Type,
                item.EffectResource, item.EffectAmount, item.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<AdminMutationResult> UpdateItemAsync(
        Guid adminId, Guid itemId, UpdateItemRequest request, CancellationToken cancellationToken)
    {
        if (!AdminRules.IsItemValid(request))
            return AdminMutationResult.Failure("Проверьте параметры предмета: цена не может превышать 1 триллион.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var item = await dbContext.Items
            .FromSqlInterpolated($"SELECT * FROM items WHERE \"Id\" = {itemId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null) return AdminMutationResult.Failure("Предмет не найден.");

        var oldValue = ItemValues(item);
        item.Price = request.Price;
        item.Description = request.Description!.Trim();
        item.EffectResource = request.EffectResource!;
        item.EffectAmount = request.EffectAmount;
        item.IsActive = request.IsActive;
        AddAudit(adminId, "item_update", "item", itemId.ToString(), oldValue,
            ItemValues(item), request.Reason!);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMutationResult.Success("Предмет обновлён.");
    }

    public async Task<IReadOnlyList<AdminActionLogResponse>> GetActionLogsAsync(CancellationToken cancellationToken) =>
        await dbContext.ActionLogs.AsNoTracking().OrderByDescending(log => log.CreatedAtUtc).Take(100)
            .Select(log => new AdminActionLogResponse(
                log.Id, log.User.Username, log.ActionType, log.Succeeded, log.Message, log.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminAuditLogResponse>> GetAuditLogsAsync(CancellationToken cancellationToken) =>
        await dbContext.AdminAuditLogs.AsNoTracking().OrderByDescending(log => log.CreatedAtUtc).Take(100)
            .Select(log => new AdminAuditLogResponse(
                log.Id, log.AdminUser.Username, log.Action, log.TargetType, log.TargetId,
                log.OldValue, log.NewValue, log.Reason, log.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    private void AddAudit(Guid adminId, string action, string targetType, string targetId,
        object oldValue, object newValue, string reason) =>
        dbContext.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(), AdminUserId = adminId, Action = action,
            TargetType = targetType, TargetId = targetId,
            OldValue = JsonSerializer.Serialize(oldValue),
            NewValue = JsonSerializer.Serialize(newValue),
            Reason = reason.Trim(), CreatedAtUtc = resourceRecovery.UtcNow
        });

    private static object Resources(GameCharacter character) => new
    {
        character.Credits, character.Energy, character.Nerve, character.Health
    };

    private static object ContractValues(Contract contract) => new
    {
        contract.CostNerve, contract.SuccessChance, contract.RewardCredits,
        contract.RewardExperience, contract.IsActive
    };

    private static object ItemValues(Item item) => new
    {
        item.Price, item.Description, item.EffectResource, item.EffectAmount, item.IsActive
    };

    private static AdminMutationResult InvalidReason() =>
        AdminMutationResult.Failure("Укажите причину длиной от 3 до 500 символов.");
}
