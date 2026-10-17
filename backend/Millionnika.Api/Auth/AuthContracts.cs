using Millionnika.Api.Game;

namespace Millionnika.Api.Auth;

public sealed record RegisterRequest(string? Username, string? Email, string? Password);
public sealed record LoginRequest(string? Login, string? Password);

public sealed record AuthResponse(string Username, string Role);

public sealed record ProfileResponse(
    string Username,
    string Description,
    string Role,
    int Level,
    long Credits,
    ResourceValue Energy,
    ResourceValue Nerve,
    ResourceValue Health,
    int Strength,
    int Reflexes,
    int Toughness,
    int Agility,
    long Experience,
    ProgressionState GeneralProgress,
    ProgressionState HackingProgress,
    ProgressionState HuntingProgress,
    IReadOnlyList<EquippedItemResponse> Equipment);

public sealed record EquippedItemResponse(
    Guid InstanceId, string Slot, string Name, string Type,
    decimal Damage, decimal Accuracy, decimal Armor, string ArmorCoverage,
    string Caliber, int ClipSize, int RateOfFireMin, int RateOfFireMax);

public sealed record ResourceValue(
    int Current,
    int Maximum,
    DateTimeOffset? NextRecoveryAtUtc);
