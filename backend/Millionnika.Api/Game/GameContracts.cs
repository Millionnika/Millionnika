namespace Millionnika.Api.Game;

public sealed record ActionRequest(Guid RequestId);
public sealed record TrainingRequest(Guid RequestId, string? Stat);

public sealed record ContractResponse(
    Guid Id,
    string Name,
    int CostNerve,
    decimal SuccessChance,
    long RewardCredits,
    long RewardExperience);

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
    string? StatName,
    int StatGain,
    PlayerStateResponse Player);

public sealed record PlayerStateResponse(
    long Credits,
    int Energy,
    int Nerve,
    int Strength,
    int Speed,
    int Defense,
    int Dexterity,
    long Experience);

public sealed record ActionLogResponse(
    string ActionType,
    bool Succeeded,
    string Message,
    DateTimeOffset CreatedAtUtc);
