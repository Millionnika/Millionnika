using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Npgsql;
using System.Text.RegularExpressions;
using Xunit;

namespace Millionnika.Api.Tests.Infrastructure;

public static class TestDatabaseSettings
{
    public const string EnvironmentVariable = "MILLIONNIKA_TEST_CONNECTION_STRING";

    // Validate before creating a context or opening any connection. Never echo the input.
    public static string Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Set {EnvironmentVariable} to a dedicated test database. No database connection was opened.");

        NpgsqlConnectionStringBuilder settings;
        try { settings = new NpgsqlConnectionStringBuilder(value); }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Invalid test connection configuration. No database connection was opened.");
        }

        if (settings.Database is null || !Regex.IsMatch(settings.Database,
                @"\Amillionnika_tests(?:_[a-z0-9]+)*\z", RegexOptions.CultureInvariant) ||
            settings.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.Equals(settings.Username, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Tests require a local database named millionnika_tests or millionnika_tests_<suffix> and a non-postgres user. No database connection was opened.");
        }

        settings.IncludeErrorDetail = false;
        settings.PersistSecurityInfo = false;
        return settings.ConnectionString;
    }
}

public static class TestDatabase
{
    private static readonly Lazy<string> Connection = new(() => TestDatabaseSettings.Validate(
        Environment.GetEnvironmentVariable(TestDatabaseSettings.EnvironmentVariable)));

    public static GameDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(Connection.Value).Options);
}

// xUnit initializes this fixture once before any of the integration test classes run.
// Classes share one database; Task.WhenAll inside tests still exercises concurrent requests.
public sealed class DatabaseFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var db = TestDatabase.CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("PostgreSQL integration")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
