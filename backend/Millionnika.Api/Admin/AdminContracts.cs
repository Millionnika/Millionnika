using Millionnika.Api.Game;

namespace Millionnika.Api.Admin;

public sealed record AdminPlayerSummary(
    Guid Id, string Username, string Email, string Role, bool IsBlocked, DateTimeOffset CreatedAtUtc);

public sealed record AdminPlayerDetails(
    Guid Id, string Username, string Email, string Role, bool IsBlocked, DateTimeOffset CreatedAtUtc,
    int Level, long Credits, int Energy, int MaxEnergy, int Nerve, int MaxNerve,
    int Health, int MaxHealth, int Strength, int Speed, int Defense, int Dexterity, long Experience);

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
    long Price, string? Description, string? EffectResource, int EffectAmount, bool IsActive, string? Reason);

public sealed record AdminContractResponse(
    Guid Id, string Name, string Description, int CostNerve, decimal SuccessChance,
    long RewardCredits, long RewardExperience, bool IsActive, string Direction,
    int MinimumLevel, int MinimumHackingLevel, long RewardHackingExperience);

public sealed record AdminItemResponse(
    Guid Id, string Name, string Description, long Price, string Type,
    string EffectResource, int EffectAmount, bool IsActive);

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
