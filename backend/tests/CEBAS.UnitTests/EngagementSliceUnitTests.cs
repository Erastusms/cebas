using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CEBAS.Api.Features.Engagements.Bookmarks.CreateBookmark;
using CEBAS.Api.Features.Engagements.Bookmarks.GetBookmarks;
using CEBAS.Api.Features.Engagements.Bookmarks.RemoveBookmark;
using CEBAS.Api.Features.Engagements.Likes.CreateLike;
using CEBAS.Api.Features.Engagements.Likes.RemoveLike;
using CEBAS.Api.Features.Posts.GetUserPosts;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Services;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.UnitTests;

public class EngagementSliceUnitTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    #region Like Slice Tests

    [Fact]
    public async Task CreateLike_ValidPostAndUser_ShouldCreateLike_AndIncrementLikeCount()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var liker = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Hello world!");

        await dbContext.Users.AddRangeAsync(author, liker);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new CreateLikeCommandHandler(dbContext, blockService, NullLogger<CreateLikeCommandHandler>.Instance);

        var result = await handler.Handle(new CreateLikeCommand(liker.Id, post.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.PostId.Should().Be(post.Id);
        result.Liked.Should().BeTrue();
        result.LikeCount.Should().Be(1);

        var persistedLike = await dbContext.PostLikes.FirstOrDefaultAsync(l => l.PostId == post.Id && l.UserId == liker.Id);
        persistedLike.Should().NotBeNull();

        var updatedPost = await dbContext.Posts.FindAsync(post.Id);
        updatedPost!.LikeCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateLike_DuplicateRequest_ShouldBeIdempotent_AndNotDoubleIncrement()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var liker = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Hello world!");

        await dbContext.Users.AddRangeAsync(author, liker);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new CreateLikeCommandHandler(dbContext, blockService, NullLogger<CreateLikeCommandHandler>.Instance);

        // First like
        var result1 = await handler.Handle(new CreateLikeCommand(liker.Id, post.Id), CancellationToken.None);
        result1.Liked.Should().BeTrue();
        result1.LikeCount.Should().Be(1);

        // Second like (duplicate)
        var result2 = await handler.Handle(new CreateLikeCommand(liker.Id, post.Id), CancellationToken.None);
        result2.Liked.Should().BeTrue();
        result2.LikeCount.Should().Be(1);

        // Third like (duplicate)
        var result3 = await handler.Handle(new CreateLikeCommand(liker.Id, post.Id), CancellationToken.None);
        result3.Liked.Should().BeTrue();
        result3.LikeCount.Should().Be(1);

        var likeCountInDb = await dbContext.PostLikes.CountAsync(l => l.PostId == post.Id && l.UserId == liker.Id);
        likeCountInDb.Should().Be(1);

        var updatedPost = await dbContext.Posts.FindAsync(post.Id);
        updatedPost!.LikeCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateLike_NonExistentPost_ShouldThrowNotFoundException()
    {
        using var dbContext = CreateDbContext();
        var liker = User.Create("bob", "bob@test.com", "hash", "Bob");
        await dbContext.Users.AddAsync(liker);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new CreateLikeCommandHandler(dbContext, blockService, NullLogger<CreateLikeCommandHandler>.Instance);

        var act = () => handler.Handle(new CreateLikeCommand(liker.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*was not found*");
    }

    [Fact]
    public async Task CreateLike_DeletedPost_ShouldThrowNotFoundException()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var liker = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Hello world!");
        post.Delete();

        await dbContext.Users.AddRangeAsync(author, liker);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new CreateLikeCommandHandler(dbContext, blockService, NullLogger<CreateLikeCommandHandler>.Instance);

        var act = () => handler.Handle(new CreateLikeCommand(liker.Id, post.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*was not found*");
    }

    [Fact]
    public async Task CreateLike_WhenBlocked_ShouldThrowForbiddenException()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var liker = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Hello world!");
        var block = Block.Create(author.Id, liker.Id); // Alice blocked Bob

        await dbContext.Users.AddRangeAsync(author, liker);
        await dbContext.Posts.AddAsync(post);
        await dbContext.Blocks.AddAsync(block);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new CreateLikeCommandHandler(dbContext, blockService, NullLogger<CreateLikeCommandHandler>.Instance);

        var act = () => handler.Handle(new CreateLikeCommand(liker.Id, post.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*privacy or block restrictions*");
    }

    [Fact]
    public async Task RemoveLike_ExistingLike_ShouldDeleteLike_AndDecrementLikeCount()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var liker = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Hello world!");
        post.IncrementLikeCount();
        var like = PostLike.Create(post.Id, liker.Id);

        await dbContext.Users.AddRangeAsync(author, liker);
        await dbContext.Posts.AddAsync(post);
        await dbContext.PostLikes.AddAsync(like);
        await dbContext.SaveChangesAsync();

        var handler = new RemoveLikeCommandHandler(dbContext, NullLogger<RemoveLikeCommandHandler>.Instance);

        var result = await handler.Handle(new RemoveLikeCommand(liker.Id, post.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.PostId.Should().Be(post.Id);
        result.Liked.Should().BeFalse();
        result.LikeCount.Should().Be(0);

        var persistedLike = await dbContext.PostLikes.FirstOrDefaultAsync(l => l.PostId == post.Id && l.UserId == liker.Id);
        persistedLike.Should().BeNull();

        var updatedPost = await dbContext.Posts.FindAsync(post.Id);
        updatedPost!.LikeCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoveLike_NonExistentLike_ShouldBeIdempotent_AndNeverMakeCountNegative()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var liker = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Hello world!");

        await dbContext.Users.AddRangeAsync(author, liker);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var handler = new RemoveLikeCommandHandler(dbContext, NullLogger<RemoveLikeCommandHandler>.Instance);

        // First unlike when never liked
        var result1 = await handler.Handle(new RemoveLikeCommand(liker.Id, post.Id), CancellationToken.None);
        result1.Liked.Should().BeFalse();
        result1.LikeCount.Should().Be(0);

        // Second unlike
        var result2 = await handler.Handle(new RemoveLikeCommand(liker.Id, post.Id), CancellationToken.None);
        result2.Liked.Should().BeFalse();
        result2.LikeCount.Should().Be(0);

        var updatedPost = await dbContext.Posts.FindAsync(post.Id);
        updatedPost!.LikeCount.Should().Be(0);
    }

    #endregion

    #region Bookmark Slice Tests

    [Fact]
    public async Task CreateBookmark_ValidPostAndUser_ShouldCreateBookmark_AndIncrementBookmarkCount()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Save this post!");

        await dbContext.Users.AddRangeAsync(author, user);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new CreateBookmarkCommandHandler(dbContext, blockService, NullLogger<CreateBookmarkCommandHandler>.Instance);

        var result = await handler.Handle(new CreateBookmarkCommand(user.Id, post.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.PostId.Should().Be(post.Id);
        result.Bookmarked.Should().BeTrue();
        result.BookmarkCount.Should().Be(1);

        var persisted = await dbContext.PostBookmarks.FirstOrDefaultAsync(b => b.PostId == post.Id && b.UserId == user.Id);
        persisted.Should().NotBeNull();

        var updatedPost = await dbContext.Posts.FindAsync(post.Id);
        updatedPost!.BookmarkCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateBookmark_DuplicateRequest_ShouldBeIdempotent()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Save this post!");

        await dbContext.Users.AddRangeAsync(author, user);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new CreateBookmarkCommandHandler(dbContext, blockService, NullLogger<CreateBookmarkCommandHandler>.Instance);

        var result1 = await handler.Handle(new CreateBookmarkCommand(user.Id, post.Id), CancellationToken.None);
        result1.Bookmarked.Should().BeTrue();
        result1.BookmarkCount.Should().Be(1);

        var result2 = await handler.Handle(new CreateBookmarkCommand(user.Id, post.Id), CancellationToken.None);
        result2.Bookmarked.Should().BeTrue();
        result2.BookmarkCount.Should().Be(1);

        (await dbContext.PostBookmarks.CountAsync()).Should().Be(1);
        (await dbContext.Posts.FindAsync(post.Id))!.BookmarkCount.Should().Be(1);
    }

    [Fact]
    public async Task RemoveBookmark_ExistingBookmark_ShouldDeleteBookmark_AndDecrementCount()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Save this post!");
        post.IncrementBookmarkCount();
        var bookmark = PostBookmark.Create(post.Id, user.Id);

        await dbContext.Users.AddRangeAsync(author, user);
        await dbContext.Posts.AddAsync(post);
        await dbContext.PostBookmarks.AddAsync(bookmark);
        await dbContext.SaveChangesAsync();

        var handler = new RemoveBookmarkCommandHandler(dbContext, NullLogger<RemoveBookmarkCommandHandler>.Instance);

        var result = await handler.Handle(new RemoveBookmarkCommand(user.Id, post.Id), CancellationToken.None);

        result.Bookmarked.Should().BeFalse();
        result.BookmarkCount.Should().Be(0);

        (await dbContext.PostBookmarks.CountAsync()).Should().Be(0);
        (await dbContext.Posts.FindAsync(post.Id))!.BookmarkCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoveBookmark_NonExistentBookmark_ShouldBeIdempotent_AndNeverMakeCountNegative()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var user = User.Create("bob", "bob@test.com", "hash", "Bob");
        var post = Post.Create(author.Id, "Save this post!");

        await dbContext.Users.AddRangeAsync(author, user);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var handler = new RemoveBookmarkCommandHandler(dbContext, NullLogger<RemoveBookmarkCommandHandler>.Instance);

        var result = await handler.Handle(new RemoveBookmarkCommand(user.Id, post.Id), CancellationToken.None);

        result.Bookmarked.Should().BeFalse();
        result.BookmarkCount.Should().Be(0);
        (await dbContext.Posts.FindAsync(post.Id))!.BookmarkCount.Should().Be(0);
    }

    #endregion

    #region GetBookmarks Slice Tests

    [Fact]
    public async Task GetBookmarks_ShouldReturnOnlyUserBookmarks_WithKeysetPagination()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("author", "author@test.com", "hash", "Author");
        var user1 = User.Create("user1", "u1@test.com", "hash", "User 1");
        var user2 = User.Create("user2", "u2@test.com", "hash", "User 2");

        var post1 = Post.Create(author.Id, "Post 1");
        var post2 = Post.Create(author.Id, "Post 2");
        var post3 = Post.Create(author.Id, "Post 3");
        var post4 = Post.Create(author.Id, "Post 4 (User2 only)");

        await dbContext.Users.AddRangeAsync(author, user1, user2);
        await dbContext.Posts.AddRangeAsync(post1, post2, post3, post4);

        // User1 bookmarks post1, post2, post3 at distinct timestamps
        var b1 = PostBookmark.Create(post1.Id, user1.Id);
        b1.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);

        var b2 = PostBookmark.Create(post2.Id, user1.Id);
        b2.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var b3 = PostBookmark.Create(post3.Id, user1.Id);
        b3.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);

        // User2 bookmarks post4
        var b4 = PostBookmark.Create(post4.Id, user2.Id);

        await dbContext.PostBookmarks.AddRangeAsync(b1, b2, b3, b4);
        await dbContext.SaveChangesAsync();

        var handler = new GetBookmarksQueryHandler(dbContext, NullLogger<GetBookmarksQueryHandler>.Instance);

        // Page 1 with limit 2 for User1
        var page1 = await handler.Handle(new GetBookmarksQuery(user1.Id, null, Limit: 2), CancellationToken.None);

        page1.Items.Should().HaveCount(2);
        page1.HasNextPage.Should().BeTrue();
        page1.NextCursor.Should().NotBeNullOrEmpty();
        page1.Items[0].PostId.Should().Be(post3.Id); // newest first
        page1.Items[1].PostId.Should().Be(post2.Id);

        // Page 2 using cursor
        var page2 = await handler.Handle(new GetBookmarksQuery(user1.Id, page1.NextCursor, Limit: 2), CancellationToken.None);

        page2.Items.Should().HaveCount(1);
        page2.HasNextPage.Should().BeFalse();
        page2.Items[0].PostId.Should().Be(post1.Id);

        // Ensure user2's bookmarked post is never in user1's results
        var allUser1PostIds = page1.Items.Select(x => x.PostId).Concat(page2.Items.Select(x => x.PostId)).ToList();
        allUser1PostIds.Should().NotContain(post4.Id);
    }

    [Fact]
    public async Task GetUserPosts_FilterLikes_ShouldReturnLikedPostsInDescendingOrder()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var liker = User.Create("bob", "bob@test.com", "hash", "Bob");

        var post1 = Post.Create(author.Id, "Post 1");
        var post2 = Post.Create(author.Id, "Post 2");

        await dbContext.Users.AddRangeAsync(author, liker);
        await dbContext.Posts.AddRangeAsync(post1, post2);

        var like1 = PostLike.Create(post1.Id, liker.Id);
        like1.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);

        var like2 = PostLike.Create(post2.Id, liker.Id);
        like2.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        await dbContext.PostLikes.AddRangeAsync(like1, like2);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetUserPostsQueryHandler(dbContext, blockService, NullLogger<GetUserPostsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetUserPostsQuery("bob", ViewerUserId: liker.Id, Filter: "likes", Limit: 10),
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(post2.Id); // most recently liked first
        result.Items[0].Liked.Should().BeTrue();
        result.Items[1].Id.Should().Be(post1.Id);
        result.Items[1].Liked.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserPosts_FilterBookmarks_ShouldEnforcePrivacy()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var bookmarkOwner = User.Create("bob", "bob@test.com", "hash", "Bob");
        var stranger = User.Create("charlie", "charlie@test.com", "hash", "Charlie");

        var post1 = Post.Create(author.Id, "Post 1");

        await dbContext.Users.AddRangeAsync(author, bookmarkOwner, stranger);
        await dbContext.Posts.AddAsync(post1);

        var bookmark1 = PostBookmark.Create(post1.Id, bookmarkOwner.Id);
        await dbContext.PostBookmarks.AddAsync(bookmark1);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetUserPostsQueryHandler(dbContext, blockService, NullLogger<GetUserPostsQueryHandler>.Instance);

        // Stranger trying to view Bob's bookmarks -> returns empty list (private)
        var strangerResult = await handler.Handle(
            new GetUserPostsQuery("bob", ViewerUserId: stranger.Id, Filter: "bookmarks", Limit: 10),
            CancellationToken.None);

        strangerResult.Should().NotBeNull();
        strangerResult.Items.Should().BeEmpty();

        // Bob viewing his own bookmarks -> returns bookmarked post
        var ownerResult = await handler.Handle(
            new GetUserPostsQuery("bob", ViewerUserId: bookmarkOwner.Id, Filter: "bookmarks", Limit: 10),
            CancellationToken.None);

        ownerResult.Should().NotBeNull();
        ownerResult.Items.Should().HaveCount(1);
        ownerResult.Items[0].Id.Should().Be(post1.Id);
        ownerResult.Items[0].Bookmarked.Should().BeTrue();
    }

    #endregion
}
