using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;

namespace Millionnika.Api.Game;

public sealed class GameActionService(
    GameDbContext dbContext,
    IRandomSource randomSource,
    IOptions<TrainingOptions> trainingOptions,
    IResourceRecoveryService resourceRecovery) : IGameActionService
{
    private const string ContractAction = "contract";
    private const string TrainingAction = "training";
    private static readonly Guid LegacyContractId = Guid.Parse("1c92d38c-6aca-4d33-9044-a4a92f6a6101");

    public Task<ContractResponse?> GetContractAsync(CancellationToken cancellationToken) =>
        dbContext.Contracts.AsNoTracking()
            .Where(contract => contract.IsActive && contract.CostNerve > 0)
            .OrderBy(contract => contract.Name).ThenBy(contract => contract.Id)
            .Select(contract => new ContractResponse(
                contract.Id, contract.Name, contract.Description, contract.CostNerve, contract.SuccessChance,
                contract.RewardCredits, contract.RewardExperience))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContractResponse>> GetContractsAsync(CancellationToken cancellationToken) =>
        await dbContext.Contracts.AsNoTracking()
            .Where(contract => contract.IsActive && contract.CostNerve > 0)
            .OrderBy(contract => contract.Name).ThenBy(contract => contract.Id)
            .Select(contract => new ContractResponse(
                contract.Id, contract.Name, contract.Description, contract.CostNerve, contract.SuccessChance,
                contract.RewardCredits, contract.RewardExperience))
            .ToListAsync(cancellationToken);

    public TrainingConfigResponse GetTrainingConfig()
    {
        var options = trainingOptions.Value;
        return new TrainingConfigResponse(options.EnergyCost, options.StatGain, options.ExperienceGain);
    }

    public async Task<GameActionResult> ExecuteContractAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return await ExecuteContractCoreAsync(userId, requestId, LegacyContractId, cancellationToken);
    }

    public Task<GameActionResult> ExecuteContractAsync(
        Guid userId, Guid requestId, Guid contractId, CancellationToken cancellationToken) =>
        ExecuteContractCoreAsync(userId, requestId, contractId, cancellationToken);

    private async Task<GameActionResult> ExecuteContractCoreAsync(
        Guid userId,
        Guid requestId,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var character = await LockCharacterAsync(userId, cancellationToken);
        var existing = await FindExistingAsync(userId, requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ActionType != ContractAction || existing.RequestSignature != ActionRequestSignature.Contract(contractId))
                return RequestConflict(requestId, character);
            resourceRecovery.Apply(character);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(existing, character, replayed: true);
        }
        resourceRecovery.Apply(character);

        var signature = ActionRequestSignature.Contract(contractId);
        var contract = contractId == Guid.Empty
            ? null
            : await dbContext.Contracts.SingleOrDefaultAsync(
                candidate => candidate.Id == contractId && candidate.IsActive, cancellationToken);
        ActionLog log;

        if (contract is null || !GameRules.IsValidContractCost(contract.CostNerve))
        {
            log = CreateLog(userId, requestId, ContractAction, signature, false,
                "Контракт сейчас недоступен. Ресурсы не списаны.", errorCode: "contract_unavailable");
        }
        else if (!GameRules.HasEnoughResource(character.Nerve, contract.CostNerve))
        {
            log = CreateLog(userId, requestId, ContractAction, signature, false,
                $"Недостаточно хладнокровия. Не хватает {contract.CostNerve - character.Nerve}.", errorCode: "insufficient_nerve");
        }
        else
        {
            var succeeded = GameRules.ContractSucceeds(randomSource.NextDouble(), contract.SuccessChance);
            var credits = character.Credits;
            var experience = character.Experience;
            if (succeeded)
            {
                if (!GameNumericLimits.TryAdd(character.Credits, contract.RewardCredits,
                        GameNumericLimits.MaxEconomyValue, out credits) ||
                    !GameNumericLimits.TryAdd(character.Experience, contract.RewardExperience,
                        GameNumericLimits.MaxEconomyValue, out experience))
                {
                    log = CreateLog(userId, requestId, ContractAction, signature, false,
                        "Награда превысила допустимый числовой предел. Ресурс не списан.",
                        errorCode: "numeric_limit");
                    dbContext.ActionLogs.Add(log);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return ToResult(log, character);
                }
            }

            if (character.Nerve == character.MaxNerve)
                character.LastNerveRecoveryAtUtc = resourceRecovery.UtcNow;
            character.Nerve -= contract.CostNerve;
            character.Credits = credits;
            character.Experience = experience;

            log = CreateLog(
                userId, requestId, ContractAction, signature, succeeded,
                succeeded ? "Дрон перехвачен. Награда получена." : "Дрон ушёл. Награды нет.",
                nerveSpent: contract.CostNerve,
                creditsAwarded: succeeded ? contract.RewardCredits : 0,
                experienceAwarded: succeeded ? contract.RewardExperience : 0);
        }

        dbContext.ActionLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(log, character);
    }

    public async Task<GameActionResult> TrainAsync(
        Guid userId,
        Guid requestId,
        TrainingStat stat,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var character = await LockCharacterAsync(userId, cancellationToken);
        var signature = ActionRequestSignature.Training(stat);
        var existing = await FindExistingAsync(userId, requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ActionType != TrainingAction || existing.RequestSignature != signature)
                return RequestConflict(requestId, character);
            resourceRecovery.Apply(character);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(existing, character, replayed: true);
        }
        resourceRecovery.Apply(character);

        var options = trainingOptions.Value;
        ActionLog log;

        if (!GameRules.HasEnoughResource(character.Energy, options.EnergyCost))
        {
            log = CreateLog(userId, requestId, TrainingAction, signature, false,
                "Недостаточно энергии.", errorCode: "insufficient_energy", statName: stat.ToString());
        }
        else
        {
            if (!GameNumericLimits.TryAdd(character.Experience, options.ExperienceGain,
                    GameNumericLimits.MaxEconomyValue, out var experience) ||
                !GameRules.TryGetIncreasedStat(character, stat, options.StatGain, out var increasedStat))
            {
                log = CreateLog(userId, requestId, TrainingAction, signature, false,
                    "Тренировка превысила допустимый числовой предел. Энергия не списана.",
                    errorCode: "numeric_limit", statName: stat.ToString());
                dbContext.ActionLogs.Add(log);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ToResult(log, character);
            }

            if (character.Energy == character.MaxEnergy)
                character.LastEnergyRecoveryAtUtc = resourceRecovery.UtcNow;
            character.Energy -= options.EnergyCost;
            character.Experience = experience;
            GameRules.SetStat(character, stat, increasedStat);

            log = CreateLog(
                userId, requestId, TrainingAction, signature, true,
                $"Тренировка завершена: {GetStatDisplayName(stat)} +{options.StatGain}.",
                energySpent: options.EnergyCost,
                experienceAwarded: options.ExperienceGain,
                statName: stat.ToString(),
                statGain: options.StatGain);
        }

        dbContext.ActionLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(log, character);
    }

    public async Task<IReadOnlyList<ActionLogResponse>> GetRecentLogsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.ActionLogs.AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.CreatedAtUtc)
            .Take(10)
            .Select(log => new ActionLogResponse(log.ActionType, log.Succeeded, log.Message, log.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    private async Task<GameCharacter> LockCharacterAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Characters
            .FromSqlInterpolated($"SELECT * FROM characters WHERE \"UserId\" = {userId} FOR UPDATE")
            .SingleAsync(cancellationToken);

    private Task<ActionLog?> FindExistingAsync(Guid userId, Guid requestId, CancellationToken cancellationToken) =>
        dbContext.ActionLogs.AsNoTracking().SingleOrDefaultAsync(
            log => log.UserId == userId && log.RequestId == requestId,
            cancellationToken);

    private static string GetStatDisplayName(TrainingStat stat) => stat switch
    {
        TrainingStat.Strength => "сила",
        TrainingStat.Speed => "скорость",
        TrainingStat.Defense => "защита",
        TrainingStat.Dexterity => "ловкость",
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };

    private static ActionLog CreateLog(
        Guid userId,
        Guid requestId,
        string actionType,
        string requestSignature,
        bool succeeded,
        string message,
        string? errorCode = null,
        int energySpent = 0,
        int nerveSpent = 0,
        long creditsAwarded = 0,
        long experienceAwarded = 0,
        string? statName = null,
        int statGain = 0) => new()
        {
            Id = Guid.NewGuid(), UserId = userId, RequestId = requestId,
            ActionType = actionType, RequestSignature = requestSignature,
            Succeeded = succeeded, ErrorCode = errorCode, Message = message,
            EnergySpent = energySpent, NerveSpent = nerveSpent,
            CreditsAwarded = creditsAwarded, ExperienceAwarded = experienceAwarded,
            StatName = statName, StatGain = statGain, CreatedAtUtc = DateTimeOffset.UtcNow
        };

    private static GameActionResult ToResult(ActionLog log, GameCharacter character, bool replayed = false) => new(
        new ActionResultResponse(
            log.RequestId, replayed, log.Succeeded, log.Message, log.EnergySpent, log.NerveSpent,
            log.CreditsAwarded, log.ExperienceAwarded, log.StatName, log.StatGain,
            new PlayerStateResponse(
                character.Credits, character.Energy, character.Nerve,
                character.Strength, character.Speed, character.Defense, character.Dexterity,
                character.Experience)),
        ResourceRejected: log.ErrorCode is "insufficient_nerve" or "insufficient_energy" or
            "numeric_limit" or "contract_unavailable");

    private static GameActionResult RequestConflict(Guid requestId, GameCharacter character) => new(
        new ActionResultResponse(
            requestId, false, false,
            "Этот requestId уже использован для другого действия или других параметров.",
            0, 0, 0, 0, null, 0,
            new PlayerStateResponse(
                character.Credits, character.Energy, character.Nerve,
                character.Strength, character.Speed, character.Defense, character.Dexterity,
                character.Experience)),
        ResourceRejected: true);
}
