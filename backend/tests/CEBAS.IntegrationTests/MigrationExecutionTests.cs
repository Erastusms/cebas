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

    [Fact]
    public void FollowsMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("010_follows.sql");

        foundPath.Should().NotBeNull("010_follows.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS follows");
        sqlContent.Should().Contain("follower_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("following_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("chk_follows_no_self_follow CHECK (follower_id <> following_id)");
        sqlContent.Should().Contain("uq_follows_follower_following UNIQUE (follower_id, following_id)");
        sqlContent.Should().Contain("idx_follows_following_created");
        sqlContent.Should().Contain("idx_follows_follower_created");
    }

    [Fact]
    public void BlocksMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("011_blocks.sql");

        foundPath.Should().NotBeNull("011_blocks.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS blocks");
        sqlContent.Should().Contain("blocker_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("blocked_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("chk_blocks_no_self_block CHECK (blocker_id <> blocked_id)");
        sqlContent.Should().Contain("uq_blocks_blocker_blocked UNIQUE (blocker_id, blocked_id)");
        sqlContent.Should().Contain("idx_blocks_blocker_id");
        sqlContent.Should().Contain("idx_blocks_blocked_id");
    }

    [Fact]
    public void PostsMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("005_posts.sql");

        foundPath.Should().NotBeNull("005_posts.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS posts");
        sqlContent.Should().Contain("author_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("reply_count INT NOT NULL DEFAULT 0");
        sqlContent.Should().Contain("media_count INT NOT NULL DEFAULT 0");
        sqlContent.Should().Contain("chk_posts_reply_count CHECK (reply_count >= 0)");
        sqlContent.Should().Contain("chk_posts_media_count CHECK (media_count >= 0 AND media_count <= 4)");
        sqlContent.Should().Contain("idx_posts_author_created");
    }

    [Fact]
    public void PostMediaMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("006_post_media.sql");

        foundPath.Should().NotBeNull("006_post_media.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS post_media");
        sqlContent.Should().Contain("post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("media_id UUID NOT NULL REFERENCES media(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("position INT NOT NULL");
        sqlContent.Should().Contain("uq_post_media_post_position UNIQUE (post_id, position)");
        sqlContent.Should().Contain("uq_post_media_post_media UNIQUE (post_id, media_id)");
        sqlContent.Should().Contain("chk_post_media_position CHECK (position >= 0 AND position < 4)");
    }

    [Fact]
    public void PostRepliesMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("007_post_replies.sql");

        foundPath.Should().NotBeNull("007_post_replies.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS post_replies");
        sqlContent.Should().Contain("post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("author_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("parent_reply_id UUID REFERENCES post_replies(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("content VARCHAR(1000) NOT NULL");
        sqlContent.Should().Contain("idx_post_replies_post_created");
        sqlContent.Should().Contain("idx_post_replies_parent_created");
    }

    [Fact]
    public void UserBannerMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("012_user_banner.sql");

        foundPath.Should().NotBeNull("012_user_banner.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("banner_url VARCHAR(500)");
        sqlContent.Should().Contain("banner_media_id UUID REFERENCES media(id)");
    }

    [Fact]
    public void NotificationsMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("012_notifications.sql");

        foundPath.Should().NotBeNull("012_notifications.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS notifications");
        sqlContent.Should().Contain("recipient_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("actor_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE");
        sqlContent.Should().Contain("type notification_type_enum NOT NULL");
        sqlContent.Should().Contain("is_read BOOLEAN NOT NULL DEFAULT FALSE");
        sqlContent.Should().Contain("idx_notifications_recipient_created");
        sqlContent.Should().Contain("idx_notifications_recipient_unread");
        sqlContent.Should().Contain("uq_notifications_like");
        sqlContent.Should().Contain("uq_notifications_follow");
    }

    [Fact]
    public void OutboxEventsMigrationScript_ShouldExistAndContainRequiredSqlDirectives()
    {
        string? foundPath = FindMigrationScript("013_outbox_events.sql");

        foundPath.Should().NotBeNull("013_outbox_events.sql migration file must exist in backend/migrations/sql/");

        var sqlContent = File.ReadAllText(foundPath!);
        sqlContent.Should().Contain("CREATE TABLE IF NOT EXISTS outbox_events");
        sqlContent.Should().Contain("event_type VARCHAR(100) NOT NULL");
        sqlContent.Should().Contain("aggregate_type VARCHAR(100) NOT NULL");
        sqlContent.Should().Contain("aggregate_id UUID NOT NULL");
        sqlContent.Should().Contain("payload JSONB NOT NULL");
        sqlContent.Should().Contain("status VARCHAR(20) NOT NULL DEFAULT 'PENDING'");
        sqlContent.Should().Contain("idx_outbox_events_polling");
        sqlContent.Should().Contain("chk_outbox_status");
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
