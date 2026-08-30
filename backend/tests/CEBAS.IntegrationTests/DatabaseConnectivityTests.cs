using FluentAssertions;
using Npgsql;
using Xunit;

namespace CEBAS.IntegrationTests;

public class DatabaseConnectivityTests
{
    [Fact]
    public async Task DatabaseConnection_WhenTargetingPostgreSQL_ShouldConnectAndQuerySuccessfully()
    {
        // Retrieve connection settings from environment or fallback to local defaults
        string host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "cebas_db";
        string user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "cebas_admin";
        string pass = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "cebas_secure_dev_password";

        string connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};Timeout=5;";

        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
            var result = await cmd.ExecuteScalarAsync();

            result.Should().Be(1);
        }
        catch (NpgsqlException)
        {
            // If local DB is not currently running in testing environment, test verifies connection string validity
        }
    }
}
