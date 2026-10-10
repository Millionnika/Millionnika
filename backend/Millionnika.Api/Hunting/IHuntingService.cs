namespace Millionnika.Api.Hunting;

public interface IHuntingService
{
    Task<IReadOnlyList<HuntingLocationResponse>> GetCatalogAsync(Guid userId, CancellationToken cancellationToken);
    Task<HuntFightResponse?> GetCurrentFightAsync(Guid userId, CancellationToken cancellationToken);
    Task<HuntingActionResult> StartAsync(Guid userId, StartHuntRequest request, CancellationToken cancellationToken);
    Task<HuntingActionResult> TurnAsync(Guid userId, HuntTurnRequest request, CancellationToken cancellationToken);
}
