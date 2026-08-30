using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using CEBAS.Application.Abstractions;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Services;

namespace CEBAS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Strongly-typed Options
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));
        services.Configure<PgBouncerOptions>(configuration.GetSection(PgBouncerOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));

        var postgresOptions = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>() ?? new PostgresOptions();
        var pgBouncerOptions = configuration.GetSection(PgBouncerOptions.SectionName).Get<PgBouncerOptions>() ?? new PgBouncerOptions();

        // Determine connection string: Prefer PgBouncer when enabled, fallback to direct Postgres
        string connectionString = pgBouncerOptions.Enabled
            ? pgBouncerOptions.BuildConnectionString(postgresOptions.Database, postgresOptions.Username, postgresOptions.Password)
            : postgresOptions.ConnectionString;

        // 2. EF Core PostgreSQL ApplicationDbContext
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<DatabaseSeeder>();

        // 3. Redis ConnectionMultiplexer (Lazy / Resilient)
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Redis");
            try
            {
                var config = ConfigurationOptions.Parse(redisOptions.ConnectionString);
                config.AbortOnConnectFail = false;
                config.ConnectTimeout = 5000;
                return ConnectionMultiplexer.Connect(config);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to connect to Redis on startup. Connection will retry in background.");
                var config = ConfigurationOptions.Parse(redisOptions.ConnectionString);
                config.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(config);
            }
        });

        // 4. Common & Auth Services
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ISessionTokenService, SessionTokenService>();

        // 5. Repositories
        services.AddScoped<IUserRepository, Persistence.Repositories.UserRepository>();
        services.AddScoped<ISessionRepository, Persistence.Repositories.SessionRepository>();

        return services;
    }
}
