using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;
using Millionnika.Api.Game;
using Millionnika.Api.Inventory;

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
                user.Character.Strength, user.Character.Reflexes, user.Character.Toughness,
                user.Character.Agility, user.Character.Experience))
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
        var energyChanged = character.Energy != request.Energy;
        var nerveChanged = character.Nerve != request.Nerve;
        character.Credits = request.Credits;
        character.Energy = request.Energy;
        character.Nerve = request.Nerve;
        character.Health = request.Health;
        if (energyChanged) character.LastEnergyRecoveryAtUtc = now;
        if (nerveChanged) character.LastNerveRecoveryAtUtc = now;
        AddAudit(adminId, "resources_update", "user", playerId.ToString(), oldValue,
            Resources(character), request.Reason!);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMutationResult.Success("Ресурсы обновлены.");
    }

    public async Task<IReadOnlyList<AdminContractResponse>> GetContractsAsync(CancellationToken cancellationToken) =>
        await dbContext.Contracts.AsNoTracking().OrderBy(contract => contract.Name)
            .Select(contract => new AdminContractResponse(
                contract.Id, contract.Name, contract.Description, contract.CostNerve, contract.SuccessChance,
                contract.RewardCredits, contract.RewardExperience, contract.IsActive, contract.Direction,
                contract.MinimumLevel, contract.MinimumHackingLevel, contract.RewardHackingExperience))
            .ToListAsync(cancellationToken);

    public async Task<AdminMutationResult> CreateContractAsync(
        Guid adminId, CreateContractRequest request, CancellationToken cancellationToken)
    {
        if (!AdminRules.IsContractValid(request))
            return InvalidContract();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            Name = request.Name!.Trim(),
            Description = request.Description!.Trim(),
            CostNerve = request.CostNerve,
            SuccessChance = request.SuccessChance,
            RewardCredits = request.RewardCredits,
            RewardExperience = request.RewardExperience,
            Direction = request.Direction,
            MinimumLevel = request.MinimumLevel,
            MinimumHackingLevel = request.MinimumHackingLevel,
            RewardHackingExperience = request.RewardHackingExperience,
            CooldownSeconds = 0,
            IsActive = request.IsActive
        };
        dbContext.Contracts.Add(contract);
        AddAudit(adminId, "contract_create", "contract", contract.Id.ToString(),
            new { Exists = false }, ContractValues(contract), request.Reason!);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMutationResult.Success("Контракт создан.");
    }

    public async Task<AdminMutationResult> UpdateContractAsync(
        Guid adminId, Guid contractId, UpdateContractRequest request, CancellationToken cancellationToken)
    {
        if (!AdminRules.IsContractValid(request))
            return InvalidContract();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var contract = await dbContext.Contracts
            .FromSqlInterpolated($"SELECT * FROM contracts WHERE \"Id\" = {contractId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (contract is null) return AdminMutationResult.Failure("Контракт не найден.");

        var oldValue = ContractValues(contract);
        if (request.Name is not null) contract.Name = request.Name.Trim();
        if (request.Description is not null) contract.Description = request.Description.Trim();
        contract.CostNerve = request.CostNerve;
        contract.SuccessChance = request.SuccessChance;
        contract.RewardCredits = request.RewardCredits;
        contract.RewardExperience = request.RewardExperience;
        contract.Direction = request.Direction;
        contract.MinimumLevel = request.MinimumLevel;
        contract.MinimumHackingLevel = request.MinimumHackingLevel;
        contract.RewardHackingExperience = request.RewardHackingExperience;
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
                item.EffectResource, item.EffectAmount, item.IsActive, item.AllowedSlots,
                item.MinimumDamage, item.MaximumDamage, item.MinimumAccuracy, item.MaximumAccuracy,
                item.MinimumArmor, item.MaximumArmor, item.ArmorCoverage,
                item.Caliber, item.ClipSize, item.RateOfFireMin, item.RateOfFireMax,
                item.CooldownSeconds, item.CooldownGroup))
            .ToListAsync(cancellationToken);

    public async Task<AdminMutationResult> CreateItemAsync(
        Guid adminId, CreateItemRequest request, CancellationToken cancellationToken)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(), Name = request.Name?.Trim() ?? string.Empty,
            Description = request.Description?.Trim() ?? string.Empty, Price = request.Price,
            Type = request.Type, EffectResource = request.EffectResource?.Trim() ?? string.Empty,
            EffectAmount = request.EffectAmount, AllowedSlots = request.AllowedSlots?.Trim() ?? string.Empty,
            CooldownSeconds = request.CooldownSeconds, CooldownGroup = request.CooldownGroup?.Trim() ?? string.Empty,
            MinimumDamage = request.MinimumDamage, MaximumDamage = request.MaximumDamage,
            MinimumAccuracy = request.MinimumAccuracy, MaximumAccuracy = request.MaximumAccuracy,
            MinimumArmor = request.MinimumArmor, MaximumArmor = request.MaximumArmor,
            ArmorCoverage = request.ArmorCoverage ?? "{}", IsActive = request.IsActive
            ,Caliber = request.Caliber?.Trim() ?? string.Empty, ClipSize = request.ClipSize,
            RateOfFireMin = request.RateOfFireMin, RateOfFireMax = request.RateOfFireMax
        };
        if (!AdminRules.IsItemValid(item, request.Reason))
            return InvalidItem();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Items.Add(item);
        AddAudit(adminId, "item_create", "item", item.Id.ToString(), new { Exists = false },
            ItemValues(item), request.Reason!);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminMutationResult.Success("Предмет создан.");
    }

    public async Task<AdminMutationResult> UpdateItemAsync(
        Guid adminId, Guid itemId, UpdateItemRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var item = await dbContext.Items
            .FromSqlInterpolated($"SELECT * FROM items WHERE \"Id\" = {itemId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null) return AdminMutationResult.Failure("Предмет не найден.");

        var oldValue = ItemValues(item);
        if (!AdminRules.IsItemValid(item, request)) return InvalidItem();
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

    public async Task<IReadOnlyList<AdminHuntingLocationResponse>> GetHuntingLocationsAsync(CancellationToken token) =>
        await dbContext.HuntingLocations.AsNoTracking().OrderBy(value => value.MinimumHuntingLevel).ThenBy(value => value.Name)
            .Select(value => new AdminHuntingLocationResponse(value.Id, value.Name, value.Description, value.MinimumHuntingLevel, value.IsActive)).ToListAsync(token);

    public async Task<AdminMutationResult> UpdateHuntingLocationAsync(Guid adminId, Guid id, UpdateHuntingLocationRequest request, CancellationToken token)
    {
        if (!AdminRules.IsReasonValid(request.Reason) || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120 ||
            string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 500 || request.MinimumHuntingLevel is < 1 or > 20)
            return AdminMutationResult.Failure("Проверьте название, описание, уровень и причину.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        var value = await dbContext.HuntingLocations.FromSqlInterpolated($"SELECT * FROM hunting_locations WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(token);
        if (value is null) return AdminMutationResult.Failure("Локация не найдена.");
        var old = new { value.Name, value.Description, value.MinimumHuntingLevel, value.IsActive };
        value.Name = request.Name.Trim(); value.Description = request.Description.Trim(); value.MinimumHuntingLevel = request.MinimumHuntingLevel; value.IsActive = request.IsActive;
        AddAudit(adminId, "hunting_location_update", "hunting_location", id.ToString(), old,
            new { value.Name, value.Description, value.MinimumHuntingLevel, value.IsActive }, request.Reason!);
        await dbContext.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return AdminMutationResult.Success("Локация обновлена.");
    }

    public async Task<IReadOnlyList<AdminHuntingTargetResponse>> GetHuntingTargetsAsync(CancellationToken token) =>
        await dbContext.HuntingTargets.AsNoTracking().OrderBy(value => value.MinimumHuntingLevel).ThenBy(value => value.Name)
            .Select(value => new AdminHuntingTargetResponse(value.Id, value.LocationId, value.Name, value.Description,
                value.MinimumHuntingLevel, value.EnergyCost, value.Health, value.Strength, value.Reflexes, value.Toughness, value.Agility,
                value.WeaponName, value.WeaponDamage, value.WeaponAccuracy, value.ClipSize, value.RateOfFireMin,
                value.RateOfFireMax, value.SpareClips, value.Armor, value.ArmorCoverage, value.RewardCredits,
                value.RewardExperience, value.RewardHuntingExperience, value.IsActive)).ToListAsync(token);

    public async Task<AdminMutationResult> UpdateHuntingTargetAsync(Guid adminId, Guid id, UpdateHuntingTargetRequest request, CancellationToken token)
    {
        if (!IsHuntingTargetValid(request) || !AdminRules.IsReasonValid(request.Reason))
            return AdminMutationResult.Failure("Проверьте характеристики робота, награды, покрытие и причину.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        if (!await dbContext.HuntingLocations.AnyAsync(value => value.Id == request.LocationId, token))
            return AdminMutationResult.Failure("Локация не найдена.");
        var value = await dbContext.HuntingTargets.FromSqlInterpolated($"SELECT * FROM hunting_targets WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(token);
        if (value is null) return AdminMutationResult.Failure("Робот не найден.");
        var old = HuntingTargetValues(value);
        value.LocationId = request.LocationId; value.Name = request.Name!.Trim(); value.Description = request.Description!.Trim();
        value.MinimumHuntingLevel = request.MinimumHuntingLevel; value.EnergyCost = request.EnergyCost; value.Health = request.Health;
        value.Strength = request.Strength; value.Reflexes = request.Reflexes; value.Toughness = request.Toughness; value.Agility = request.Agility;
        value.WeaponName = request.WeaponName!.Trim(); value.WeaponDamage = request.WeaponDamage; value.WeaponAccuracy = request.WeaponAccuracy;
        value.ClipSize = request.ClipSize; value.RateOfFireMin = request.RateOfFireMin; value.RateOfFireMax = request.RateOfFireMax; value.SpareClips = request.SpareClips;
        value.Armor = request.Armor; value.ArmorCoverage = request.ArmorCoverage!; value.RewardCredits = request.RewardCredits;
        value.RewardExperience = request.RewardExperience; value.RewardHuntingExperience = request.RewardHuntingExperience; value.IsActive = request.IsActive;
        AddAudit(adminId, "hunting_target_update", "hunting_target", id.ToString(), old, HuntingTargetValues(value), request.Reason!);
        await dbContext.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return AdminMutationResult.Success("Робот обновлён.");
    }

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
        character.Credits, character.Energy, character.Nerve, character.Health,
        character.LastEnergyRecoveryAtUtc, character.LastNerveRecoveryAtUtc
    };

    private static object ContractValues(Contract contract) => new
    {
        contract.Name, contract.Description, contract.CostNerve, contract.SuccessChance, contract.RewardCredits,
        contract.RewardExperience, contract.Direction, contract.MinimumLevel, contract.MinimumHackingLevel,
        contract.RewardHackingExperience, contract.IsActive
    };

    private static AdminMutationResult InvalidContract() =>
        AdminMutationResult.Failure(
            "Проверьте название, описание и параметры контракта: стоимость должна быть больше нуля, награды — не более 1 триллиона.");

    private static object ItemValues(Item item) => new
    {
        item.Name, item.Price, item.Description, item.Type, item.EffectResource, item.EffectAmount,
        item.AllowedSlots, item.MinimumDamage, item.MaximumDamage, item.MinimumAccuracy,
        item.MaximumAccuracy, item.MinimumArmor, item.MaximumArmor, item.ArmorCoverage,
        item.Caliber, item.ClipSize, item.RateOfFireMin, item.RateOfFireMax,
        item.CooldownSeconds, item.CooldownGroup, item.IsActive
    };

    private static AdminMutationResult InvalidItem() => AdminMutationResult.Failure(
        "Проверьте тип, слоты, цену, диапазоны рейтингов, покрытие и причину изменения предмета.");

    private static AdminMutationResult InvalidReason() =>
        AdminMutationResult.Failure("Укажите причину длиной от 3 до 500 символов.");

    private static bool IsHuntingTargetValid(UpdateHuntingTargetRequest value) =>
        value.LocationId != Guid.Empty && !string.IsNullOrWhiteSpace(value.Name) && value.Name.Trim().Length <= 120 &&
        !string.IsNullOrWhiteSpace(value.Description) && value.Description.Trim().Length <= 500 &&
        !string.IsNullOrWhiteSpace(value.WeaponName) && value.WeaponName.Trim().Length <= 120 &&
        value.MinimumHuntingLevel is >= 1 and <= 20 && value.EnergyCost > 0 && value.Health > 0 &&
        value.Strength > 0 && value.Reflexes > 0 && value.Toughness > 0 && value.Agility > 0 &&
        value.WeaponDamage is >= 1 and <= 1000 && value.WeaponAccuracy is >= 0 and <= 100 &&
        value.ClipSize is >= 0 and <= 1000 && value.RateOfFireMin >= 0 && value.RateOfFireMax >= value.RateOfFireMin &&
        value.RateOfFireMax <= Math.Max(value.ClipSize, 0) && value.SpareClips is >= 0 and <= 3 &&
        value.Armor is >= 0 and <= 100 && (value.Armor == 0 ? value.ArmorCoverage == "{}" : EquipmentRules.IsCoverageValid(value.ArmorCoverage)) &&
        value.RewardCredits is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        value.RewardExperience is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        value.RewardHuntingExperience is >= 0 and <= GameNumericLimits.MaxEconomyValue;

    private static object HuntingTargetValues(HuntingTarget value) => new
    {
        value.LocationId, value.Name, value.Description, value.MinimumHuntingLevel, value.EnergyCost, value.Health,
        value.Strength, value.Reflexes, value.Toughness, value.Agility, value.WeaponName, value.WeaponDamage,
        value.WeaponAccuracy, value.ClipSize, value.RateOfFireMin, value.RateOfFireMax, value.SpareClips,
        value.Armor, value.ArmorCoverage, value.RewardCredits, value.RewardExperience, value.RewardHuntingExperience, value.IsActive
    };
}
