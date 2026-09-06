using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Npgsql;
using CEBAS.Api;
using CEBAS.Api.Configuration;
using CEBAS.Api.Middleware;
using CEBAS.Application;
using CEBAS.Infrastructure;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;

// 1. Serilog Early Bootstrap Logger with Sensitive Data Masking
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.With<SensitiveDataMaskingEnricher>()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CEBAS API application...");

    var builder = WebApplication.CreateBuilder(args);

    // 2. Configure Serilog with Host
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.With<SensitiveDataMaskingEnricher>()
        .WriteTo.Console(new CompactJsonFormatter()));

    // 3. Register Layer Dependency Injections
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddApi(builder.Configuration);

    // 4. Register OpenTelemetry Tracing & Prometheus Metrics
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddSource(CebasActivitySource.SourceName)
            .AddAspNetCoreInstrumentation(opts =>
            {
                opts.RecordException = true;
                opts.Filter = httpContext =>
                {
                    var path = httpContext.Request.Path.Value ?? string.Empty;
                    return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) &&
                           !path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) &&
                           !path.StartsWith("/readyz", StringComparison.OrdinalIgnoreCase) &&
                           !path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
                };
            })
            .AddHttpClientInstrumentation()
            .AddNpgsql())
        .WithMetrics(metrics => metrics
            .AddMeter(CebasMetrics.MeterName)
            .AddMeter(TimelineMetrics.MeterName)
            .AddMeter(OutboxMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter());

    var app = builder.Build();

    // 4. Auto-run Database Migrations & Seeding on Startup (Development & Testing)
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        try
        {
            var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
            await migrator.MigrateAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database auto-migration/seed skipped on startup (DB might be offline or still starting up): {Message}", ex.Message);
        }
    }

    // 5. Middleware Pipeline
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "CEBAS API v1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseCors(CorsOptions.PolicyName);
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.UseOpenTelemetryPrometheusScrapingEndpoint();
    app.MapControllers();
    app.MapHub<CEBAS.Api.Hubs.SocialHub>("/hubs/social");

    Log.Information("CEBAS API initialized successfully. Listening on configured ports.");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CEBAS API terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
