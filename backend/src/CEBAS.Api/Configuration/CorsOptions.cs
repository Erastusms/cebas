namespace CEBAS.Api.Configuration;

public class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "DefaultCorsPolicy";

    public string[] AllowedOrigins { get; set; } = ["http://localhost:3000", "http://127.0.0.1:3000"];
    public bool AllowCredentials { get; set; } = true;
}
