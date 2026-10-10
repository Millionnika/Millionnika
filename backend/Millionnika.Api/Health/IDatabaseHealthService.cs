namespace Millionnika.Api.Health;

public interface IDatabaseHealthService
{
    Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken);
}

public sealed record DatabaseHealthResult(bool Available, string Message);
