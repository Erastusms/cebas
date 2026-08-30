namespace CEBAS.Infrastructure.Configuration;

public class PostgresOptions
{
    public const string SectionName = "Postgres";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "cebas_db";
    public string Username { get; set; } = "cebas_admin";
    public string Password { get; set; } = "cebas_secure_dev_password";

    public string ConnectionString =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Include Error Detail=true;";
}
