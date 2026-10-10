using Millionnika.Api.Game;

namespace Millionnika.Api.Auth;

public sealed record RegisterRequest(string? Username, string? Email, string? Password);
public sealed record LoginRequest(string? Login, string? Password);

public sealed record AuthResponse(string Username, string Role);

public sealed record ProfileResponse(
    string Username,
    string Role,
    int Level,
    long Credits,
    ResourceValue Energy,
    ResourceValue Nerve,
    ResourceValue Health,
    int Strength,
    int Speed,
    int Defense,
    int Dexterity,
    long Experience,
    ProgressionState GeneralProgress,
    ProgressionState HackingProgress);

public sealed record ResourceValue(
    int Current,
    int Maximum,
    DateTimeOffset? NextRecoveryAtUtc);
