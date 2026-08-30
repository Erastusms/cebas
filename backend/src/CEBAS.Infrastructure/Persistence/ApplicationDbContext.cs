using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Common;

namespace CEBAS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Extensions configuration
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("citext");

        // Custom PostgreSQL ENUMs mapping
        modelBuilder.HasPostgresEnum("user_role_enum", ["USER", "MODERATOR", "ADMIN"]);
        modelBuilder.HasPostgresEnum("media_type_enum", ["IMAGE", "VIDEO", "AUDIO"]);
        modelBuilder.HasPostgresEnum("media_status_enum", ["UPLOADING", "READY", "FAILED", "DELETED"]);
        modelBuilder.HasPostgresEnum("notification_type_enum", ["POST_LIKED", "POST_REPLIED", "REPLY_LIKED", "USER_FOLLOWED", "USER_MENTIONED"]);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
