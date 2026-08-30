using FluentAssertions;
using Xunit;

namespace CEBAS.IntegrationTests;

public class MigrationExecutionTests
{
    [Fact]
    public void ExtensionsMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("001_extensions.sql");

        foundPath.Should().NotBeNull("001_extensions.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("uuid-ossp");
        sqlContent.Should().Contain("citext");
        sqlContent.Should().Contain("user_role_enum");
        sqlContent.Should().Contain("media_type_enum");
        sqlContent.Should().Contain("media_status_enum");
        sqlContent.Should().Contain("notification_type_enum");
    }

    [Fact]
    public void UsersMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("002_users.sql");

        foundPath.Should().NotBeNull("002_users.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS users");
        sqlContent.Should().Contain("password_hash");
        sqlContent.Should().Contain("idx_users_username_lower");
        sqlContent.Should().Contain("idx_users_email_lower");
        sqlContent.Should().Contain("LOWER(username)");
        sqlContent.Should().Contain("LOWER(email)");
    }

    [Fact]
    public void SessionsMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("004_sessions.sql");

        foundPath.Should().NotBeNull("004_sessions.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS sessions");
        sqlContent.Should().Contain("token_hash");
        sqlContent.Should().Contain("idx_sessions_token_hash");
        sqlContent.Should().Contain("idx_sessions_user_id");
    }

    private static string? FindMigrationScript(string filename)
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            var candidate1 = Path.Combine(currentDir.FullName, "migrations", "sql", filename);
            if (File.Exists(candidate1)) return candidate1;

            var candidate2 = Path.Combine(currentDir.FullName, "backend", "migrations", "sql", filename);
            if (File.Exists(candidate2)) return candidate2;

            currentDir = currentDir.Parent;
        }

        return null;
    }
}
