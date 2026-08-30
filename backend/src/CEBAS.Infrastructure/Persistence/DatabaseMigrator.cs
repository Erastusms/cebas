using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CEBAS.Infrastructure.Persistence;

/// <summary>
/// Executes initial database bootstrap scripts and EF Core migrations.
/// </summary>
public class DatabaseMigrator
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(ApplicationDbContext dbContext, ILogger<DatabaseMigrator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task MigrateAsync(string? migrationsSqlPath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting database initialization and migration sequence...");

            // Execute raw SQL extensions script if available
            string sqlScriptPath = migrationsSqlPath ?? FindSqlScriptPath();
            if (File.Exists(sqlScriptPath))
            {
                _logger.LogInformation("Executing baseline migration script from: {Path}", sqlScriptPath);
                var sql = await File.ReadAllTextAsync(sqlScriptPath, cancellationToken);
                await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                _logger.LogInformation("Baseline migration script executed successfully.");
            }
            else
            {
                _logger.LogWarning("Migration script not found at path: {Path}. Attempting EF migration check...", sqlScriptPath);
            }

            // If EF Core migrations are configured in future phases, apply them
            var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying pending EF Core migrations: {Migrations}", string.Join(", ", pendingMigrations));
                await _dbContext.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("EF Core migrations applied successfully.");
            }

            _logger.LogInformation("Database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during database migration: {Message}", ex.Message);
            throw;
        }
    }

    private static string FindSqlScriptPath()
    {
        // Try multiple probe paths for development, docker, and test runners
        var probePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "migrations", "sql", "001_extensions.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "migrations", "sql", "001_extensions.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "migrations", "sql", "001_extensions.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "migrations", "sql", "001_extensions.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "migrations", "sql", "001_extensions.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "migrations", "sql", "001_extensions.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "migrations", "sql", "001_extensions.sql")
        };

        foreach (var path in probePaths)
        {
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return probePaths[0];
    }
}
