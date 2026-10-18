using Millionnika.Api.Game;

namespace Millionnika.Api.Admin;

public sealed record AdminPlayerSummary(
    Guid Id, string Username, string Email, string Role, bool IsBlocked, DateTimeOffset CreatedAtUtc);

public sealed record AdminPlayerDetails(
    Guid Id, string Username, string Email, string Role, bool IsBlocked, DateTimeOffset CreatedAtUtc,
    int Level, long Credits, int Energy, int MaxEnergy, int Nerve, int MaxNerve,
    int Health, int MaxHealth, int Strength, int Reflexes, int Toughness, int Agility, long Experience);

public sealed record SetBlockRequest(bool IsBlocked, string? Reason);
public sealed record UpdateResourcesRequest(long Credits, int Energy, int Nerve, int Health, string? Reason);
public sealed record UpdateContractRequest(
    int CostNerve, decimal SuccessChance, long RewardCredits, long RewardExperience, bool IsActive, string? Reason,
    string? Name = null, string? Description = null, string Direction = ContractDirections.Normal,
    int MinimumLevel = 1, int MinimumHackingLevel = 1, long RewardHackingExperience = 0);
public sealed record CreateContractRequest(
    string? Name, string? Description, int CostNerve, decimal SuccessChance,
    long RewardCredits, long RewardExperience, bool IsActive, string? Reason,
    string Direction = ContractDirections.Normal, int MinimumLevel = 1,
    int MinimumHackingLevel = 1, long RewardHackingExperience = 0);
public sealed record UpdateItemRequest(
    long Price, string? Description, string? EffectResource, int EffectAmount, bool IsActive, string? Reason,
    decimal MinimumDamage = 0, decimal MaximumDamage = 0,
    decimal MinimumAccuracy = 0, decimal MaximumAccuracy = 0,
    decimal MinimumArmor = 0, decimal MaximumArmor = 0, string ArmorCoverage = "{}",
    string Caliber = "", int ClipSize = 0, int RateOfFireMin = 0, int RateOfFireMax = 0,
    int CooldownSeconds = 0, string? CooldownGroup = null);
public sealed record CreateItemRequest(
    string? Name, string? Description, long Price, string Type, string? EffectResource,
    int EffectAmount, string AllowedSlots, decimal MinimumDamage, decimal MaximumDamage,
    decimal MinimumAccuracy, decimal MaximumAccuracy, decimal MinimumArmor, decimal MaximumArmor,
    string ArmorCoverage, bool IsActive, string? Reason,
    string Caliber = "", int ClipSize = 0, int RateOfFireMin = 0, int RateOfFireMax = 0,
    int CooldownSeconds = 0, string? CooldownGroup = null);

public sealed record AdminContractResponse(
    Guid Id, string Name, string Description, int CostNerve, decimal SuccessChance,
    long RewardCredits, long RewardExperience, bool IsActive, string Direction,
    int MinimumLevel, int MinimumHackingLevel, long RewardHackingExperience);

public sealed record AdminItemResponse(
    Guid Id, string Name, string Description, long Price, string Type,
    string EffectResource, int EffectAmount, bool IsActive, string AllowedSlots,
    decimal MinimumDamage, decimal MaximumDamage, decimal MinimumAccuracy, decimal MaximumAccuracy,
    decimal MinimumArmor, decimal MaximumArmor, string ArmorCoverage,
    string Caliber, int ClipSize, int RateOfFireMin, int RateOfFireMax,
    int CooldownSeconds, string CooldownGroup);

public sealed record AdminActionLogResponse(
    Guid Id, string Username, string ActionType, bool Succeeded, string Message, DateTimeOffset CreatedAtUtc);

public sealed record AdminAuditLogResponse(
    Guid Id, string AdminUsername, string Action, string TargetType, string TargetId,
    string OldValue, string NewValue, string Reason, DateTimeOffset CreatedAtUtc);

public sealed record AdminMutationResult(bool Succeeded, string Message)
{
    public static AdminMutationResult Success(string message) => new(true, message);
    public static AdminMutationResult Failure(string message) => new(false, message);
}

public sealed record AdminHuntingLocationResponse(Guid Id, string Name, string Description, int MinimumHuntingLevel, bool IsActive);
public sealed record UpdateHuntingLocationRequest(string? Name, string? Description, int MinimumHuntingLevel, bool IsActive, string? Reason);
public sealed record AdminHuntingTargetResponse(Guid Id, string Name, string Description,
    int MinimumHuntingLevel, int EnergyCost, int Health, int Strength, int Reflexes, int Toughness, int Agility,
    string WeaponName, decimal WeaponDamage, decimal WeaponAccuracy, int ClipSize, int RateOfFireMin,
    int RateOfFireMax, int SpareClips, decimal Armor, string ArmorCoverage, long RewardCredits,
    long RewardExperience, long RewardHuntingExperience, bool IsActive,
    long MinimumCredits, long MaximumCredits, IReadOnlyList<AdminHuntingLootRowResponse> LootRows);
public sealed record AdminHuntingLootRowResponse(Guid ItemId, string ItemName, decimal ChancePercent,
    int MinimumQuantity, int MaximumQuantity);
public sealed record UpdateHuntingLootRowRequest(Guid ItemId, decimal ChancePercent,
    int MinimumQuantity, int MaximumQuantity);
public sealed record UpdateHuntingTargetRequest(string? Name, string? Description,
    int MinimumHuntingLevel, int EnergyCost, int Health, int Strength, int Reflexes, int Toughness, int Agility,
    string? WeaponName, decimal WeaponDamage, decimal WeaponAccuracy, int ClipSize, int RateOfFireMin,
    int RateOfFireMax, int SpareClips, decimal Armor, string? ArmorCoverage, long RewardCredits,
    long RewardExperience, long RewardHuntingExperience, bool IsActive, string? Reason,
    long MinimumCredits, long MaximumCredits, IReadOnlyList<UpdateHuntingLootRowRequest>? LootRows);
