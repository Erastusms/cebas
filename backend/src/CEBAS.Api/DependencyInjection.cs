using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using CEBAS.Api.Configuration;

namespace CEBAS.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Controllers & JSON serialization
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

        // 2. CORS Policy
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, builder =>
            {
                if (corsOptions.AllowedOrigins.Length > 0)
                {
                    builder.WithOrigins(corsOptions.AllowedOrigins)
                           .AllowAnyMethod()
                           .AllowAnyHeader();

                    if (corsOptions.AllowCredentials)
                    {
                        builder.AllowCredentials();
                    }
                }
                else
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
            });
        });

        // 3. Authentication & Authorization
        services.AddHttpContextAccessor();
        services.AddScoped<CEBAS.Application.Abstractions.ICurrentUser, Services.CurrentUser>();
        services.AddScoped<Services.ICookieService, Services.CookieService>();

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = Authentication.CookieSessionAuthenticationHandler.SchemeName;
            options.DefaultAuthenticateScheme = Authentication.CookieSessionAuthenticationHandler.SchemeName;
            options.DefaultChallengeScheme = Authentication.CookieSessionAuthenticationHandler.SchemeName;
        })
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Authentication.CookieSessionAuthenticationHandler>(
            Authentication.CookieSessionAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("ModeratorOnly", policy =>
                policy.RequireRole("MODERATOR"));
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole("ADMIN"));
            options.AddPolicy("ModeratorOrAdmin", policy =>
                policy.RequireRole("MODERATOR", "ADMIN"));
        });

        // 3.1 Distributed Rate Limiting Middleware
        RateLimiting.RateLimitingRegistration.AddDistributedRateLimiting(services, configuration);


        // 4. Swagger / OpenAPI Documentation
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
            {
                Title = "CEBAS API",
                Version = "v1",
                Description = "Celoteh Bebas — High-concurrency, real-time social platform API engine.",
                Contact = new Microsoft.OpenApi.OpenApiContact
                {
                    Name = "CEBAS Core Platform Engineering Team"
                }
            });

            // Include XML comments if generated
            var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        // 5. MediatR CQRS & Pipeline Behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(MediatR.IPipelineBehavior<,>), typeof(Common.Behaviors.LoggingBehavior<,>));
            cfg.AddBehavior(typeof(MediatR.IPipelineBehavior<,>), typeof(Common.Behaviors.ValidationBehavior<,>));
        });

        // 6. FluentValidation
        FluentValidation.ServiceCollectionExtensions.AddValidatorsFromAssembly(services, typeof(DependencyInjection).Assembly);

        // 7. SignalR with Redis Scale-Out Backplane (Resilient Fallback)
        var signalRBuilder = services.AddSignalR();
        var redisOptions = configuration.GetSection(CEBAS.Infrastructure.Configuration.RedisOptions.SectionName)
            .Get<CEBAS.Infrastructure.Configuration.RedisOptions>() ?? new CEBAS.Infrastructure.Configuration.RedisOptions();

        if (redisOptions.Enabled && !string.IsNullOrWhiteSpace(redisOptions.Host))
        {
            bool isRedisAvailable = false;
            try
            {
                var probeConfig = StackExchange.Redis.ConfigurationOptions.Parse(redisOptions.ConnectionString);
                probeConfig.ConnectTimeout = 1000;
                probeConfig.SyncTimeout = 1000;
                probeConfig.AbortOnConnectFail = true;
                using var probeMux = StackExchange.Redis.ConnectionMultiplexer.Connect(probeConfig);
                isRedisAvailable = probeMux.IsConnected;
            }
            catch
            {
                isRedisAvailable = false;
            }

            if (isRedisAvailable)
            {
                signalRBuilder.AddStackExchangeRedis(redisOptions.ConnectionString, options =>
                {
                    options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("cebas:signalr");
                });
                Serilog.Log.Information("SignalR configured with StackExchangeRedis backplane ({Host}:{Port}).", redisOptions.Host, redisOptions.Port);
            }
            else
            {
                Serilog.Log.Warning("Redis is unreachable at {Host}:{Port}. SignalR using standalone in-memory HubLifetimeManager.", redisOptions.Host, redisOptions.Port);
            }
        }
        else
        {
            Serilog.Log.Information("Redis is disabled. SignalR using standalone in-memory HubLifetimeManager.");
        }

        services.AddHostedService<Services.RedisEventDispatcherService>();

        return services;
    }
}
