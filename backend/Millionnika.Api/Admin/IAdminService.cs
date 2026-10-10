namespace Millionnika.Api.Admin;

public interface IAdminService
{
    Task<IReadOnlyList<AdminPlayerSummary>> GetPlayersAsync(CancellationToken cancellationToken);
    Task<AdminPlayerDetails?> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken);
    Task<AdminMutationResult> SetBlockAsync(Guid adminId, Guid playerId, SetBlockRequest request, CancellationToken cancellationToken);
    Task<AdminMutationResult> UpdateResourcesAsync(Guid adminId, Guid playerId, UpdateResourcesRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminContractResponse>> GetContractsAsync(CancellationToken cancellationToken);
    Task<AdminMutationResult> CreateContractAsync(Guid adminId, CreateContractRequest request, CancellationToken cancellationToken);
    Task<AdminMutationResult> UpdateContractAsync(Guid adminId, Guid contractId, UpdateContractRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminItemResponse>> GetItemsAsync(CancellationToken cancellationToken);
    Task<AdminMutationResult> UpdateItemAsync(Guid adminId, Guid itemId, UpdateItemRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminActionLogResponse>> GetActionLogsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAuditLogResponse>> GetAuditLogsAsync(CancellationToken cancellationToken);
}
