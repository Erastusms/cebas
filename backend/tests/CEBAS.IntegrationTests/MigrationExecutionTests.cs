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
