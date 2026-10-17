namespace Millionnika.Api.Game;

public sealed record ActionRequest(Guid RequestId, Guid ContractId);
public sealed record TrainingRequest(Guid RequestId, string? Stat);

public sealed record ContractResponse(
    Guid Id,
    string Name,
    string Description,
    int CostNerve,
    decimal SuccessChance,
    long RewardCredits,
    long RewardExperience,
    string Direction,
    int MinimumLevel,
    int MinimumHackingLevel,
    long RewardHackingExperience,
    bool IsAvailable,
    string? LockReason);

public sealed record TrainingConfigResponse(int EnergyCost, int StatGain, int ExperienceGain);

public sealed record ActionResultResponse(
    Guid RequestId,
    bool Replayed,
    bool Succeeded,
    string Message,
    int EnergySpent,
    int NerveSpent,
    long CreditsAwarded,
    long ExperienceAwarded,
    long HackingExperienceAwarded,
    string? StatName,
    int StatGain,
    PlayerStateResponse Player);

public sealed record PlayerStateResponse(
    long Credits,
    int Energy,
    int Nerve,
    int Strength,
    int Reflexes,
    int Toughness,
    int Agility,
    long Experience,
    int Level,
    long HackingExperience,
    int HackingLevel);

public sealed record ActionLogResponse(
    string ActionType,
    bool Succeeded,
    string Message,
    DateTimeOffset CreatedAtUtc);
