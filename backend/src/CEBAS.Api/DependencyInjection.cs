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

        // 3. Swagger / OpenAPI Documentation
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

        return services;
    }
}
