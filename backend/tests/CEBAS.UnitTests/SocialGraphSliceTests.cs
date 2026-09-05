using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CEBAS.Api.Features.SocialGraph.BlockUser;
using CEBAS.Api.Features.SocialGraph.FollowUser;
using CEBAS.Api.Features.SocialGraph.GetFollowers;
using CEBAS.Api.Features.SocialGraph.GetFollowing;
using CEBAS.Api.Features.SocialGraph.UnblockUser;
using CEBAS.Api.Features.SocialGraph.UnfollowUser;
using CEBAS.Api.Features.Users.GetPublicProfile;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Services;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.UnitTests;

public class SocialGraphSliceTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task FollowUser_ValidUsers_ShouldCreateFollowRelationship()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user2 = User.Create("bob", "bob@test.com", "hash", "Bob");
        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new FollowUserCommandHandler(dbContext, blockService, NullLogger<FollowUserCommandHandler>.Instance);

        var result = await handler.Handle(new FollowUserCommand(user1.Id, user2.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.TargetUserId.Should().Be(user2.Id);
        result.IsFollowing.Should().BeTrue();
        result.IsBlocked.Should().BeFalse();

        var persisted = await dbContext.Follows.FirstOrDefaultAsync(f => f.FollowerId == user1.Id && f.FollowingId == user2.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task FollowUser_SelfFollow_ShouldThrowValidationException()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new FollowUserCommandHandler(dbContext, blockService, NullLogger<FollowUserCommandHandler>.Instance);

        var act = () => handler.Handle(new FollowUserCommand(user.Id, user.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Values.SelectMany(v => v).Should().ContainMatch("*cannot follow themselves*");
    }

    [Fact]
    public async Task FollowUser_NonExistentTarget_ShouldThrowNotFoundException()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new FollowUserCommandHandler(dbContext, blockService, NullLogger<FollowUserCommandHandler>.Instance);

        var act = () => handler.Handle(new FollowUserCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    public async Task FollowUser_WhenBlockedByTarget_ShouldThrowForbiddenException()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user2 = User.Create("bob", "bob@test.com", "hash", "Bob");
        var block = Block.Create(user2.Id, user1.Id); // Bob blocked Alice

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.Blocks.AddAsync(block);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new FollowUserCommandHandler(dbContext, blockService, NullLogger<FollowUserCommandHandler>.Instance);

        var act = () => handler.Handle(new FollowUserCommand(user1.Id, user2.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*privacy or block restrictions*");
    }

    [Fact]
    public async Task FollowUser_WhenActorBlockedTarget_ShouldThrowForbiddenException()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user2 = User.Create("bob", "bob@test.com", "hash", "Bob");
        var block = Block.Create(user1.Id, user2.Id); // Alice blocked Bob

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.Blocks.AddAsync(block);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new FollowUserCommandHandler(dbContext, blockService, NullLogger<FollowUserCommandHandler>.Instance);

        var act = () => handler.Handle(new FollowUserCommand(user1.Id, user2.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*privacy or block restrictions*");
    }

    [Fact]
    public async Task FollowUser_DuplicateFollow_ShouldBeIdempotent()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user2 = User.Create("bob", "bob@test.com", "hash", "Bob");
        var follow = Follow.Create(user1.Id, user2.Id);

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.Follows.AddAsync(follow);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new FollowUserCommandHandler(dbContext, blockService, NullLogger<FollowUserCommandHandler>.Instance);

        var result = await handler.Handle(new FollowUserCommand(user1.Id, user2.Id), CancellationToken.None);

        result.IsFollowing.Should().BeTrue();
        (await dbContext.Follows.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UnfollowUser_ExistingFollow_ShouldRemoveRelationship()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user2 = User.Create("bob", "bob@test.com", "hash", "Bob");
        var follow = Follow.Create(user1.Id, user2.Id);

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.Follows.AddAsync(follow);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new UnfollowUserCommandHandler(dbContext, blockService, NullLogger<UnfollowUserCommandHandler>.Instance);

        var result = await handler.Handle(new UnfollowUserCommand(user1.Id, user2.Id), CancellationToken.None);

        result.IsFollowing.Should().BeFalse();
        var exists = await dbContext.Follows.AnyAsync(f => f.FollowerId == user1.Id && f.FollowingId == user2.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task UnfollowUser_NonExistentFollow_ShouldBeSafeAndIdempotent()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user2 = User.Create("bob", "bob@test.com", "hash", "Bob");

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new UnfollowUserCommandHandler(dbContext, blockService, NullLogger<UnfollowUserCommandHandler>.Instance);

        var result = await handler.Handle(new UnfollowUserCommand(user1.Id, user2.Id), CancellationToken.None);

        result.IsFollowing.Should().BeFalse();
    }

    [Fact]
    public async Task BlockUser_MutualFollows_ShouldCreateBlock_AndPurgeMutualFollows()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user2 = User.Create("bob", "bob@test.com", "hash", "Bob");

        // Mutual follow: Alice follows Bob, Bob follows Alice
        var follow1 = Follow.Create(user1.Id, user2.Id);
        var follow2 = Follow.Create(user2.Id, user1.Id);

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.Follows.AddRangeAsync(follow1, follow2);
        await dbContext.SaveChangesAsync();

        var handler = new BlockUserCommandHandler(dbContext, NullLogger<BlockUserCommandHandler>.Instance);
        var result = await handler.Handle(new BlockUserCommand(user1.Id, user2.Id), CancellationToken.None);

        result.IsBlocked.Should().BeTrue();
        result.IsFollowing.Should().BeFalse();

        // Check block created
        var blockExists = await dbContext.Blocks.AnyAsync(b => b.BlockerId == user1.Id && b.BlockedId == user2.Id);
        blockExists.Should().BeTrue();

        // Check BOTH follow relationships are permanently deleted
        var remainingFollows = await dbContext.Follows.ToListAsync();
        remainingFollows.Should().BeEmpty();
    }

    [Fact]
    public async Task BlockUser_SelfBlock_ShouldThrowValidationException()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var handler = new BlockUserCommandHandler(dbContext, NullLogger<BlockUserCommandHandler>.Instance);
        var act = () => handler.Handle(new BlockUserCommand(user.Id, user.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Values.SelectMany(v => v).Should().ContainMatch("*cannot block themselves*");
    }

    [Fact]
    public async Task UnblockUser_ShouldRemoveBlock_AndNOTRestoreFollows()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user2 = User.Create("bob", "bob@test.com", "hash", "Bob");
        var block = Block.Create(user1.Id, user2.Id);

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.Blocks.AddAsync(block);
        await dbContext.SaveChangesAsync();

        var handler = new UnblockUserCommandHandler(dbContext, NullLogger<UnblockUserCommandHandler>.Instance);
        var result = await handler.Handle(new UnblockUserCommand(user1.Id, user2.Id), CancellationToken.None);

        result.IsBlocked.Should().BeFalse();
        result.IsFollowing.Should().BeFalse();

        var blockExists = await dbContext.Blocks.AnyAsync(b => b.BlockerId == user1.Id && b.BlockedId == user2.Id);
        blockExists.Should().BeFalse();

        // Invariant: follows remain non-existent
        var follows = await dbContext.Follows.ToListAsync();
        follows.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFollowers_ShouldReturnFollowers_WithKeysetPagination()
    {
        using var dbContext = CreateDbContext();
        var target = User.Create("creator", "creator@test.com", "hash", "Creator");
        var follower1 = User.Create("follower1", "f1@test.com", "hash", "Follower 1");
        var follower2 = User.Create("follower2", "f2@test.com", "hash", "Follower 2");
        var follower3 = User.Create("follower3", "f3@test.com", "hash", "Follower 3");

        await dbContext.Users.AddRangeAsync(target, follower1, follower2, follower3);

        var f1 = Follow.Create(follower1.Id, target.Id);
        f1.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);

        var f2 = Follow.Create(follower2.Id, target.Id);
        f2.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var f3 = Follow.Create(follower3.Id, target.Id);
        f3.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);

        await dbContext.Follows.AddRangeAsync(f1, f2, f3);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetFollowersQueryHandler(dbContext, blockService, NullLogger<GetFollowersQueryHandler>.Instance);

        // First page with limit 2
        var page1 = await handler.Handle(new GetFollowersQuery(target.Id, null, null, Limit: 2), CancellationToken.None);

        page1.Items.Should().HaveCount(2);
        page1.HasNextPage.Should().BeTrue();
        page1.NextCursor.Should().NotBeNullOrEmpty();

        // Second page using nextCursor
        var page2 = await handler.Handle(new GetFollowersQuery(target.Id, null, page1.NextCursor, Limit: 2), CancellationToken.None);

        page2.Items.Should().HaveCount(1);
        page2.HasNextPage.Should().BeFalse();
        page2.NextCursor.Should().BeNull();

        // Verify distinct items
        var allFetchedIds = page1.Items.Select(x => x.Id).Concat(page2.Items.Select(x => x.Id)).ToList();
        allFetchedIds.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task GetFollowers_WhenBlockedByFollower_ShouldExcludeBlockedFollower()
    {
        using var dbContext = CreateDbContext();
        var target = User.Create("target", "target@test.com", "hash", "Target");
        var viewer = User.Create("viewer", "viewer@test.com", "hash", "Viewer");
        var blockedFollower = User.Create("baduser", "bad@test.com", "hash", "Bad User");

        await dbContext.Users.AddRangeAsync(target, viewer, blockedFollower);

        var f1 = Follow.Create(viewer.Id, target.Id);
        var f2 = Follow.Create(blockedFollower.Id, target.Id);
        var block = Block.Create(viewer.Id, blockedFollower.Id); // Viewer blocked BadUser

        await dbContext.Follows.AddRangeAsync(f1, f2);
        await dbContext.Blocks.AddAsync(block);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetFollowersQueryHandler(dbContext, blockService, NullLogger<GetFollowersQueryHandler>.Instance);

        var result = await handler.Handle(new GetFollowersQuery(target.Id, viewer.Id, null, Limit: 10), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(viewer.Id);
        result.Items.Should().NotContain(x => x.Id == blockedFollower.Id);
    }

    [Fact]
    public async Task GetPublicProfile_WithFollowsAndBlocks_ShouldReturnLiveStatsAndRelationship()
    {
        using var dbContext = CreateDbContext();
        var target = User.Create("bob", "bob@test.com", "hash", "Bob");
        var viewer = User.Create("alice", "alice@test.com", "hash", "Alice");
        var thirdParty = User.Create("charlie", "charlie@test.com", "hash", "Charlie");

        await dbContext.Users.AddRangeAsync(target, viewer, thirdParty);

        // Viewer follows Target, ThirdParty follows Target, Target follows Viewer
        var f1 = Follow.Create(viewer.Id, target.Id);
        var f2 = Follow.Create(thirdParty.Id, target.Id);
        var f3 = Follow.Create(target.Id, viewer.Id);

        await dbContext.Follows.AddRangeAsync(f1, f2, f3);
        await dbContext.SaveChangesAsync();

        var handler = new GetPublicProfileQueryHandler(dbContext);
        var profile = await handler.Handle(new GetPublicProfileQuery("bob", viewer.Id), CancellationToken.None);

        profile.Should().NotBeNull();
        profile.Stats.FollowerCount.Should().Be(2);
        profile.Stats.FollowingCount.Should().Be(1);
        profile.Relationship.Should().NotBeNull();
        profile.Relationship!.IsFollowing.Should().BeTrue();
        profile.Relationship.IsFollowedBy.Should().BeTrue();
        profile.Relationship.IsBlocked.Should().BeFalse();
        profile.Relationship.IsBlockedBy.Should().BeFalse();
    }

    [Fact]
    public async Task GetPublicProfile_WhenFollowingSuspendedUser_ShouldExcludeFromFollowingCount()
    {
        using var dbContext = CreateDbContext();
        var alice = User.Create("alice", "alice@test.com", "hash", "Alice Walker");
        var johnDoe = User.Create("johndoe", "johndoe@test.com", "hash", "John Doe");
        var bob = User.Create("bob", "bob@test.com", "hash", "Bob Active");

        // John Doe gets suspended
        johnDoe.Suspend("Community standards violation");

        await dbContext.Users.AddRangeAsync(alice, johnDoe, bob);

        // Alice follows John Doe (suspended) and Bob (active)
        var f1 = Follow.Create(alice.Id, johnDoe.Id);
        var f2 = Follow.Create(alice.Id, bob.Id);

        // John Doe (suspended) also followed Alice
        var f3 = Follow.Create(johnDoe.Id, alice.Id);

        await dbContext.Follows.AddRangeAsync(f1, f2, f3);
        await dbContext.SaveChangesAsync();

        var handler = new GetPublicProfileQueryHandler(dbContext);
        var profile = await handler.Handle(new GetPublicProfileQuery("alice", alice.Id), CancellationToken.None);

        profile.Should().NotBeNull();
        // Alice only has 1 active following (Bob), not John Doe
        profile.Stats.FollowingCount.Should().Be(1);
        // Alice has 0 active followers (John Doe is suspended)
        profile.Stats.FollowerCount.Should().Be(0);
    }
}
