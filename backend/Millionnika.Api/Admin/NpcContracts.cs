namespace Millionnika.Api.Admin;

public sealed record AdminNpcResponse(
    Guid Id, string Source, string Category, string Name, string Description, bool IsActive,
    string? PortraitPath, string? FigurePath, int? Health, int? Strength, int? Toughness,
    int? Reflexes, int? Agility, string? WeaponName, decimal? WeaponDamage,
    decimal? WeaponAccuracy, decimal? Armor, string? ArmorCoverage,
    IReadOnlyDictionary<string, Millionnika.Api.Hunting.NpcZoneProtection>? ArmorZones = null,
    IReadOnlyList<AdminHuntingLootRowResponse>? LootRows = null);

public sealed record SaveNpcRequest(
    string? Category, string? Name, string? Description, bool IsActive,
    string? PortraitPath, string? FigurePath, int? Health, int? Strength, int? Toughness,
    int? Reflexes, int? Agility, string? WeaponName, decimal? WeaponDamage,
    decimal? WeaponAccuracy, decimal? Armor, string? ArmorCoverage, string? Reason,
    Dictionary<string, Millionnika.Api.Hunting.NpcZoneProtection>? ArmorZones = null,
    IReadOnlyList<UpdateHuntingLootRowRequest>? LootRows = null);

public sealed record HuntingAssignmentResponse(Guid LocationId, Guid NpcId, string Name, string Category, bool IsActive);
public sealed record HuntingSetupRequest(
    int MinimumHuntingLevel, int EnergyCost, int ClipSize, int RateOfFireMin, int RateOfFireMax,
    int SpareClips, long RewardCredits, long MinimumCredits, long MaximumCredits,
    long RewardExperience, long RewardHuntingExperience);
public sealed record AssignHuntingNpcRequest(Guid NpcId, string? Comment = null,
    HuntingSetupRequest? Setup = null, string? Reason = null);
public sealed record RemoveHuntingNpcRequest(string? Comment = null, string? Reason = null);
