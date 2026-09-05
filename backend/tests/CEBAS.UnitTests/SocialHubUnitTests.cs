using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CEBAS.Api.Hubs;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.UnitTests;

public class SocialHubUnitTests
{
    private class FakeBlockIsolationService : IBlockIsolationService
    {
        public HashSet<(Guid, Guid)> BlockedPairs { get; } = new();

        public Task<bool> IsBlockedBidirectionalAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BlockedPairs.Contains((userA, userB)) || BlockedPairs.Contains((userB, userA)));
        }

        public Task<bool> HasBlockedAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BlockedPairs.Contains((blockerId, blockedId)));
        }

        public Task<HashSet<Guid>> GetBidirectionalBlockedUserIdsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var result = new HashSet<Guid>();
            foreach (var (u1, u2) in BlockedPairs)
            {
                if (u1 == userId) result.Add(u2);
                if (u2 == userId) result.Add(u1);
            }
            return Task.FromResult(result);
        }
    }

    private class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> Added { get; } = new();
        public List<(string ConnectionId, string GroupName)> Removed { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private class FakeHubCallerContext : HubCallerContext
    {
        public override string ConnectionId { get; }
        public override ClaimsPrincipal User { get; }
        public override string? UserIdentifier => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        public override IDictionary<object, object?> Items => new Dictionary<object, object?>();
        public override IFeatureCollection Features => null!;
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public FakeHubCallerContext(string connectionId, ClaimsPrincipal user)
        {
            ConnectionId = connectionId;
            User = user;
        }

        public override void Abort() { }
    }

    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task OnConnectedAsync_ShouldAddClientToPrivateUserGroup()
    {
        using var db = CreateInMemoryDbContext();
        var blockService = new FakeBlockIsolationService();
        var groups = new FakeGroupManager();
        var userId = Guid.NewGuid();

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var context = new FakeHubCallerContext("conn-1", principal);

        var hub = new SocialHub(db, blockService, NullLogger<SocialHub>.Instance)
        {
            Context = context,
            Groups = groups
        };

        await hub.OnConnectedAsync();

        groups.Added.Should().ContainSingle(g => g.ConnectionId == "conn-1" && g.GroupName == $"user:{userId}");
    }

    [Fact]
    public async Task JoinPostGroup_WhenNotBlocked_ShouldAddToPostGroup()
    {
        using var db = CreateInMemoryDbContext();
        var blockService = new FakeBlockIsolationService();
        var groups = new FakeGroupManager();
        var userId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var post = Post.Create(authorId, "Test Post Content");
        await db.Posts.AddAsync(post);
        await db.SaveChangesAsync();

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var context = new FakeHubCallerContext("conn-2", new ClaimsPrincipal(new ClaimsIdentity(claims)));

        var hub = new SocialHub(db, blockService, NullLogger<SocialHub>.Instance)
        {
            Context = context,
            Groups = groups
        };

        await hub.JoinPostGroup(post.Id);

        groups.Added.Should().Contain(g => g.ConnectionId == "conn-2" && g.GroupName == $"post:{post.Id}");
    }

    [Fact]
    public async Task JoinPostGroup_WhenBlockedByAuthor_ShouldThrowHubException()
    {
        using var db = CreateInMemoryDbContext();
        var blockService = new FakeBlockIsolationService();
        var groups = new FakeGroupManager();
        var userId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var post = Post.Create(authorId, "Test Post Content");
        await db.Posts.AddAsync(post);
        await db.SaveChangesAsync();

        // Block relationship exists
        blockService.BlockedPairs.Add((authorId, userId));

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var context = new FakeHubCallerContext("conn-3", new ClaimsPrincipal(new ClaimsIdentity(claims)));

        var hub = new SocialHub(db, blockService, NullLogger<SocialHub>.Instance)
        {
            Context = context,
            Groups = groups
        };

        var act = async () => await hub.JoinPostGroup(post.Id);

        await act.Should().ThrowAsync<HubException>()
            .WithMessage("*privacy restrictions*");

        groups.Added.Should().NotContain(g => g.GroupName == $"post:{post.Id}");
    }
}
