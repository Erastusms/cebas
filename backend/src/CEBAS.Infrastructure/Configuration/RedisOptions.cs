namespace CEBAS.Infrastructure.Configuration;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string? Password { get; set; }

    public string ConnectionString =>
        string.IsNullOrWhiteSpace(Password)
            ? $"{Host}:{Port},abortConnect=false"
            : $"{Host}:{Port},password={Password},abortConnect=false";
}
