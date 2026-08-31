using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Common;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Media> Media => Set<Media>();

    private readonly MediatR.IPublisher? _publisher;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, MediatR.IPublisher? publisher = null)
        : base(options)
    {
        _publisher = publisher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Extensions configuration
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("citext");

        // Custom PostgreSQL ENUMs mapping
        modelBuilder.HasPostgresEnum<UserRole>("user_role_enum");
        modelBuilder.HasPostgresEnum<MediaStatus>("media_status_enum");
        modelBuilder.HasPostgresEnum("media_type_enum", ["IMAGE", "VIDEO", "AUDIO"]);
        modelBuilder.HasPostgresEnum("notification_type_enum", ["POST_LIKED", "POST_REPLIED", "REPLY_LIKED", "USER_FOLLOWED", "USER_MENTIONED"]);

        // Apply entity configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Update modified timestamps
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        // 2. Extract domain events from tracked entities
        var domainEvents = ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .SelectMany(e =>
            {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        // 3. Persist state changes
        int result = await base.SaveChangesAsync(cancellationToken);

        // 4. Publish in-process domain events
        if (_publisher != null && domainEvents.Count > 0)
        {
            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent, cancellationToken);
            }
        }

        return result;
    }
}
