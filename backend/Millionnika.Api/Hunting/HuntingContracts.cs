namespace Millionnika.Api.Hunting;

public sealed record StartHuntRequest(Guid RequestId, Guid TargetId, Guid LocationId = default);
public sealed record HuntTurnRequest(Guid RequestId, string? Action, string? Slot);

public sealed record HuntingTargetResponse(
    Guid Id, string Name, string Description, int MinimumHuntingLevel, int EnergyCost,
    int Health, long RewardCredits, long RewardExperience, long RewardHuntingExperience,
    bool Unlocked, string? LockedReason,
    long MinimumCredits = 0, long MaximumCredits = 0);
public sealed record HuntingLocationResponse(
    Guid Id, string Name, string Description, int MinimumHuntingLevel, bool Unlocked,
    IReadOnlyList<HuntingTargetResponse> Targets);
public sealed record HuntWeaponResponse(string Slot, string Name, int AmmoInClip, int ReserveAmmo, int ClipSize);
public sealed record HuntParticipantResponse(
    string Side, string Name, string Type, int Health, int MaximumHealth);
public sealed record HuntEquipmentResponse(
    string Slot, string Type, string Name, decimal Damage, decimal Accuracy, decimal Armor,
    IReadOnlyDictionary<string, decimal> Coverage);
public sealed record HuntProtectionResponse(
    string Source, decimal Armor, IReadOnlyDictionary<string, decimal> Coverage,
    IReadOnlyDictionary<string, decimal>? ZoneProtection = null);
public sealed record HuntLootResponse(Guid ItemId, string Name, string Type, int Quantity);
public sealed record HuntFightResponse(
    Guid FightId, Guid RequestId, bool Replayed, string Status, int Turn, int MaximumTurns,
    int PlayerHealth, int MaximumPlayerHealth, int RobotHealth, int MaximumRobotHealth,
    DateTimeOffset ExpiresAtUtc, DateTimeOffset? NextActionAtUtc, string Message,
    IReadOnlyList<string> Log, IReadOnlyList<HuntWeaponResponse> Weapons,
    long CreditsAwarded, long ExperienceAwarded, long HuntingExperienceAwarded,
    int HuntingLevel, int Energy, bool LevelIncreased,
    string? ErrorCode = null, string? RequiredAmmoWeapon = null, string? RequiredAmmoName = null,
    IReadOnlyList<HuntParticipantResponse>? Participants = null,
    IReadOnlyList<HuntEquipmentResponse>? PlayerEquipment = null,
    IReadOnlyList<HuntEquipmentResponse>? RobotEquipment = null,
    IReadOnlyList<HuntProtectionResponse>? RobotProtection = null,
    IReadOnlyList<HuntLootResponse>? Loot = null);
public sealed record HuntingActionResult(HuntFightResponse Response, int StatusCode);

internal sealed record HuntSnapshot(
    Guid TargetId, string TargetName, int EnergyCost, int MinimumHuntingLevel, int StartingHuntingLevel,
    int PlayerMaximumHealth, CombatStats PlayerStats, IReadOnlyList<CombatArmor> PlayerArmor,
    int RobotMaximumHealth, CombatStats RobotStats, CombatWeapon RobotWeapon,
    IReadOnlyList<CombatArmor> RobotArmor, long RewardCredits, long RewardExperience,
    long RewardHuntingExperience, string PlayerName = "Игрок",
    IReadOnlyList<HuntEquipmentSnapshot>? PlayerEquipment = null,
    long? MinimumCredits = null, long? MaximumCredits = null,
    IReadOnlyList<HuntLootSnapshot>? LootRows = null);
internal sealed record HuntLootSnapshot(Guid ItemId, string Name, string Type, decimal ChancePercent,
    int MinimumQuantity, int MaximumQuantity);
internal sealed record HuntEquipmentSnapshot(
    string Slot, string Type, string Name, decimal Damage, decimal Accuracy, decimal Armor,
    Dictionary<string, decimal> Coverage);
internal sealed record HuntState(List<HuntWeaponState> Weapons, HuntWeaponState RobotWeapon);
internal sealed record HuntWeaponState(
    Guid ItemId, Guid AmmoItemId, string Slot, string Name, decimal Damage, decimal Accuracy, string Caliber,
    int ClipSize, int RateMin, int RateMax, int AmmoInClip, int ReserveAmmo)
{
    public CombatWeapon ToCombat() => new(Slot, Name, Damage, Accuracy, Caliber, ClipSize,
        RateMin, RateMax, AmmoInClip, ReserveAmmo);
}
