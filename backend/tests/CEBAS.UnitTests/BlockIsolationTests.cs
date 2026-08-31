using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Services;

namespace CEBAS.UnitTests;

public class BlockIsolationTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task IsBlockedBidirectionalAsync_WhenBlockExistsInEitherDirection_ShouldReturnTrue()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("user_one", "u1@test.com", "hash", "User 1");
        var user2 = User.Create("user_two", "u2@test.com", "hash", "User 2");
        var user3 = User.Create("user_three", "u3@test.com", "hash", "User 3");

        var block1 = Block.Create(user1.Id, user2.Id); // User1 blocked User2

        await dbContext.Users.AddRangeAsync(user1, user2, user3);
        await dbContext.Blocks.AddAsync(block1);
        await dbContext.SaveChangesAsync();

        var service = new BlockIsolationService(dbContext);

        // Direction 1: user1 -> user2
        var isBlocked1 = await service.IsBlockedBidirectionalAsync(user1.Id, user2.Id);
        isBlocked1.Should().BeTrue();

        // Direction 2: user2 -> user1 (blocked by user1)
        var isBlocked2 = await service.IsBlockedBidirectionalAsync(user2.Id, user1.Id);
        isBlocked2.Should().BeTrue();

        // Direction 3: unblocked pair user1 -> user3
        var isBlocked3 = await service.IsBlockedBidirectionalAsync(user1.Id, user3.Id);
        isBlocked3.Should().BeFalse();
    }

    [Fact]
    public async Task HasBlockedAsync_ShouldCheckActiveBlockInitiator()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("user_alpha", "u1@test.com", "hash", "User 1");
        var user2 = User.Create("user_beta", "u2@test.com", "hash", "User 2");

        var block = Block.Create(user1.Id, user2.Id);

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.Blocks.AddAsync(block);
        await dbContext.SaveChangesAsync();

        var service = new BlockIsolationService(dbContext);

        (await service.HasBlockedAsync(user1.Id, user2.Id)).Should().BeTrue();
        (await service.HasBlockedAsync(user2.Id, user1.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GetBidirectionalBlockedUserIdsAsync_ShouldReturnCombinedBlocklist()
    {
        using var dbContext = CreateDbContext();
        var current = User.Create("user_current", "curr@test.com", "hash", "Current");
        var blockedByMe = User.Create("user_blocked_by_me", "b1@test.com", "hash", "Blocked By Me");
        var blockingMe = User.Create("user_blocking_me", "b2@test.com", "hash", "Blocking Me");
        var unrelated = User.Create("user_unrelated", "u@test.com", "hash", "Unrelated");

        await dbContext.Users.AddRangeAsync(current, blockedByMe, blockingMe, unrelated);
        await dbContext.Blocks.AddRangeAsync(
            Block.Create(current.Id, blockedByMe.Id),
            Block.Create(blockingMe.Id, current.Id)
        );
        await dbContext.SaveChangesAsync();

        var service = new BlockIsolationService(dbContext);
        var set = await service.GetBidirectionalBlockedUserIdsAsync(current.Id);

        set.Should().HaveCount(2);
        set.Should().Contain(blockedByMe.Id);
        set.Should().Contain(blockingMe.Id);
        set.Should().NotContain(unrelated.Id);
    }
}
