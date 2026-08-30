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

    public async Task MigrateAsync(string? migrationsSqlDirectory = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting database initialization and migration sequence...");

            // Discover all SQL migration scripts in order
            var sqlFiles = migrationsSqlDirectory != null && Directory.Exists(migrationsSqlDirectory)
                ? Directory.GetFiles(migrationsSqlDirectory, "*.sql").OrderBy(f => Path.GetFileName(f)).ToList()
                : FindSqlScriptPaths();

            if (sqlFiles.Count > 0)
            {
                foreach (var sqlPath in sqlFiles)
                {
                    _logger.LogInformation("Executing migration script from: {Path}", sqlPath);
                    var sql = await File.ReadAllTextAsync(sqlPath, cancellationToken);
                    await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                    _logger.LogInformation("Migration script {File} executed successfully.", Path.GetFileName(sqlPath));
                }
            }
            else
            {
                _logger.LogWarning("No SQL migration scripts found. Attempting EF migration check...");
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

    private static List<string> FindSqlScriptPaths()
    {
        // Try multiple probe directory paths for development, docker, and test runners
        var probeDirs = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "migrations", "sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "migrations", "sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "migrations", "sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "migrations", "sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "migrations", "sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "migrations", "sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "migrations", "sql")
        };

        foreach (var dir in probeDirs)
        {
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.sql").OrderBy(f => Path.GetFileName(f)).ToList();
                if (files.Count > 0)
                {
                    return files;
                }
            }
        }

        return new List<string>();
    }
}
