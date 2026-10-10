using Npgsql;

namespace Millionnika.Api.Health;

public sealed class PostgreSqlHealthService(
    IConfiguration configuration,
    ILogger<PostgreSqlHealthService> logger) : IDatabaseHealthService
{
    private const string ConnectionStringName = "GameDatabase";

    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DatabaseHealthResult(false, "Подключение к базе ещё не настроено");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return new DatabaseHealthResult(true, "PostgreSQL доступна");
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            logger.LogWarning(exception, "Database health check failed");
            return new DatabaseHealthResult(false, "PostgreSQL недоступна");
        }
    }
}
