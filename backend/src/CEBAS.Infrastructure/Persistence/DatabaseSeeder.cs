using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(ApplicationDbContext dbContext, IPasswordHasher passwordHasher, ILogger<DatabaseSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _dbContext.Users.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Database already contains user records. Skipping seed.");
                return;
            }

            _logger.LogInformation("Seeding initial users and administrative accounts into cebas_db...");

            string defaultPasswordHash = _passwordHasher.Hash("Password123!");

            var seedUsers = new List<User>
            {
                User.Create(
                    username: "admin",
                    email: "admin@cebas.io",
                    passwordHash: defaultPasswordHash,
                    displayName: "CEBAS Admin",
                    bio: "Official Administrator of the Celoteh Bebas platform.",
                    role: UserRole.Admin
                ),
                User.Create(
                    username: "moderator",
                    email: "moderator@cebas.io",
                    passwordHash: defaultPasswordHash,
                    displayName: "Community Moderator",
                    bio: "Keeping conversations civil, respectful, and safe.",
                    role: UserRole.Moderator
                ),
                User.Create(
                    username: "johndoe",
                    email: "johndoe@example.com",
                    passwordHash: defaultPasswordHash,
                    displayName: "John Doe",
                    bio: "Full-stack engineer building high-concurrency systems on .NET 10 & Next.js.",
                    role: UserRole.User
                ),
                User.Create(
                    username: "janedoe",
                    email: "janedoe@example.com",
                    passwordHash: defaultPasswordHash,
                    displayName: "Jane Doe",
                    bio: "Product designer and open-source enthusiast. Excited for unhindered social conversation!",
                    role: UserRole.User
                ),
                User.Create(
                    username: "alice",
                    email: "alice@example.com",
                    passwordHash: defaultPasswordHash,
                    displayName: "Alice Walker",
                    bio: "Cybersecurity researcher & cryptography hobbyist.",
                    role: UserRole.User
                ),
                User.Create(
                    username: "bob",
                    email: "bob@example.com",
                    passwordHash: defaultPasswordHash,
                    displayName: "Bob Smith",
                    bio: "Frontend developer exploring UI micro-interactions and accessibility.",
                    role: UserRole.User
                )
            };

            await _dbContext.Users.AddRangeAsync(seedUsers, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Database seeding completed successfully. {Count} users created.", seedUsers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Seeder encountered an issue: {Message}", ex.Message);
        }
    }
}
