namespace Millionnika.Api.Game;

public interface IGameActionService
{
    Task<ContractResponse?> GetContractAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ContractResponse>> GetContractsAsync(CancellationToken cancellationToken);
    TrainingConfigResponse GetTrainingConfig();
    Task<GameActionResult> ExecuteContractAsync(Guid userId, Guid requestId, CancellationToken cancellationToken);
    Task<GameActionResult> ExecuteContractAsync(Guid userId, Guid requestId, Guid contractId, CancellationToken cancellationToken);
    Task<GameActionResult> TrainAsync(Guid userId, Guid requestId, TrainingStat stat, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionLogResponse>> GetRecentLogsAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record GameActionResult(ActionResultResponse Response, bool ResourceRejected);
