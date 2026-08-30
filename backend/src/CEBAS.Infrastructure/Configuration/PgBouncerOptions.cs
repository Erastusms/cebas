namespace CEBAS.Infrastructure.Configuration;

public class PgBouncerOptions
{
    public const string SectionName = "PgBouncer";

    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6432;
    public string PoolMode { get; set; } = "transaction";
    public int MaxClientConnections { get; set; } = 100;
    public int DefaultPoolSize { get; set; } = 20;

    public string BuildConnectionString(string database, string username, string password) =>
        $"Host={Host};Port={Port};Database={database};Username={username};Password={password};Pooling=false;Include Error Detail=true;";
}
