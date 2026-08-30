using Serilog;
using Serilog.Events;
using CEBAS.Api;
using CEBAS.Api.Configuration;
using CEBAS.Api.Middleware;
using CEBAS.Application;
using CEBAS.Infrastructure;
using CEBAS.Infrastructure.Persistence;

// 1. Serilog Early Bootstrap Logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
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
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));

    // 3. Register Layer Dependency Injections
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddApi(builder.Configuration);

    var app = builder.Build();

    // 4. Auto-run Database Migrations / Extensions on Startup (Development & Testing)
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        try
        {
            var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
            await migrator.MigrateAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database auto-migration skipped on startup (DB might be offline or still starting up): {Message}", ex.Message);
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
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

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
