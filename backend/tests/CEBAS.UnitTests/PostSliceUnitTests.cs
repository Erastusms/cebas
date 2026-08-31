using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CEBAS.Api.Features.Posts.CreatePost;
using CEBAS.Api.Features.Posts.CreateReply;
using CEBAS.Api.Features.Posts.DeletePost;
using CEBAS.Api.Features.Posts.DeleteReply;
using CEBAS.Api.Features.Posts.GetPost;
using CEBAS.Api.Features.Posts.GetReplies;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Services;
using MediaEntity = CEBAS.Domain.Entities.Media;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.UnitTests;

public class PostSliceUnitTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static MediaEntity CreateReadyMedia(Guid ownerUserId, string fileName = "photo.png")
    {
        var media = MediaEntity.Create(ownerUserId, fileName, $"posts/{Guid.NewGuid():N}.png", "image/png", 1024 * 100);
        media.Confirm();
        return media;
    }

    #region Entity Tests

    [Fact]
    public void Post_Create_WithValidText_ShouldSucceed()
    {
        var authorId = Guid.NewGuid();
        var post = Post.Create(authorId, "Hello CEBAS world!", 0);

        post.Should().NotBeNull();
        post.AuthorId.Should().Be(authorId);
        post.Content.Should().Be("Hello CEBAS world!");
        post.MediaCount.Should().Be(0);
        post.ReplyCount.Should().Be(0);
        post.IsDeleted.Should().BeFalse();
        post.DomainEvents.Should().ContainSingle(e => e is Domain.Events.PostCreatedDomainEvent);
    }

    [Fact]
    public void Post_Create_WithOnlyMedia_ShouldSucceed()
    {
        var authorId = Guid.NewGuid();
        var post = Post.Create(authorId, null, 2);

        post.Should().NotBeNull();
        post.Content.Should().BeEmpty();
        post.MediaCount.Should().Be(2);
    }

    [Fact]
    public void Post_Create_WithEmptyContentAndZeroMedia_ShouldThrowValidationException()
    {
        var authorId = Guid.NewGuid();
        var act = () => Post.Create(authorId, "   ", 0);

        var ex = act.Should().Throw<ValidationException>().Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*cannot be empty*");
    }

    [Fact]
    public void Post_Create_Exceeding1000Characters_ShouldThrowValidationException()
    {
        var authorId = Guid.NewGuid();
        var longContent = new string('a', 1001);
        var act = () => Post.Create(authorId, longContent, 0);

        var ex = act.Should().Throw<ValidationException>().Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*cannot exceed 1000 characters*");
    }

    [Fact]
    public void Post_Create_Exceeding4Media_ShouldThrowValidationException()
    {
        var authorId = Guid.NewGuid();
        var act = () => Post.Create(authorId, "Text", 5);

        var ex = act.Should().Throw<ValidationException>().Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*between 0 and 4*");
    }

    [Fact]
    public void Post_SoftDelete_ShouldSetIsDeletedAndEmitEvent()
    {
        var post = Post.Create(Guid.NewGuid(), "To be deleted", 0);
        post.Delete();

        post.IsDeleted.Should().BeTrue();
        post.DeletedAt.Should().NotBeNull();
        post.DomainEvents.Should().Contain(e => e is Domain.Events.PostDeletedDomainEvent);
    }

    [Fact]
    public void PostMedia_Create_InvalidPositions_ShouldThrowValidationException()
    {
        var postId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();

        var actNegative = () => PostMedia.Create(postId, mediaId, -1);
        actNegative.Should().Throw<ValidationException>();

        var actTooHigh = () => PostMedia.Create(postId, mediaId, 4);
        actTooHigh.Should().Throw<ValidationException>();
    }

    [Fact]
    public void PostMedia_Create_ValidPositions_ShouldSucceed()
    {
        var postId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();

        for (int i = 0; i < 4; i++)
        {
            var pm = PostMedia.Create(postId, mediaId, i);
            pm.Position.Should().Be(i);
        }
    }

    [Fact]
    public void PostReply_Create_Valid_ShouldSucceed()
    {
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var reply = PostReply.Create(postId, authorId, "Great post!");

        reply.Should().NotBeNull();
        reply.PostId.Should().Be(postId);
        reply.AuthorId.Should().Be(authorId);
        reply.ParentReplyId.Should().BeNull();
        reply.Content.Should().Be("Great post!");
        reply.IsDeleted.Should().BeFalse();
        reply.DomainEvents.Should().ContainSingle(e => e is Domain.Events.ReplyCreatedDomainEvent);
    }

    [Fact]
    public void PostReply_Create_EmptyContent_ShouldThrowValidationException()
    {
        var act = () => PostReply.Create(Guid.NewGuid(), Guid.NewGuid(), "   ");
        act.Should().Throw<ValidationException>().Which.Errors.Values.SelectMany(v => v)
            .Should().ContainMatch("*cannot be empty*");
    }

    #endregion

    #region CreatePost Slice Tests

    [Fact]
    public async Task CreatePost_WithValidTextAndMedia_ShouldPersistAtomically()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(author);

        var media1 = CreateReadyMedia(author.Id, "img1.png");
        var media2 = CreateReadyMedia(author.Id, "img2.png");
        await dbContext.Media.AddRangeAsync(media1, media2);
        await dbContext.SaveChangesAsync();

        var handler = new CreatePostCommandHandler(dbContext, NullLogger<CreatePostCommandHandler>.Instance);
        var command = new CreatePostCommand(author.Id, "Exciting photo update!", new List<Guid> { media1.Id, media2.Id });

        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.Content.Should().Be("Exciting photo update!");
        response.MediaCount.Should().Be(2);
        response.Media.Should().HaveCount(2);
        response.Media[0].Position.Should().Be(0);
        response.Media[1].Position.Should().Be(1);
        response.Author.Username.Should().Be("alice");

        var persistedPost = await dbContext.Posts.Include(p => p.MediaAttachments).FirstOrDefaultAsync(p => p.Id == response.Id);
        persistedPost.Should().NotBeNull();
        persistedPost!.MediaAttachments.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreatePost_WithDuplicateMediaIds_ShouldThrowValidationException()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(author);

        var media = CreateReadyMedia(author.Id, "img.png");
        await dbContext.Media.AddAsync(media);
        await dbContext.SaveChangesAsync();

        var handler = new CreatePostCommandHandler(dbContext, NullLogger<CreatePostCommandHandler>.Instance);
        var command = new CreatePostCommand(author.Id, "Text", new List<Guid> { media.Id, media.Id });

        var act = () => handler.Handle(command, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Values.SelectMany(v => v).Should().ContainMatch("*Duplicate*");
    }

    [Fact]
    public async Task CreatePost_WithUnownedMedia_ShouldThrowForbiddenException()
    {
        using var dbContext = CreateDbContext();
        var authorA = User.Create("alice", "alice@test.com", "hash", "Alice");
        var authorB = User.Create("bob", "bob@test.com", "hash", "Bob");
        await dbContext.Users.AddRangeAsync(authorA, authorB);

        var bobMedia = CreateReadyMedia(authorB.Id, "bob_img.png");
        await dbContext.Media.AddAsync(bobMedia);
        await dbContext.SaveChangesAsync();

        var handler = new CreatePostCommandHandler(dbContext, NullLogger<CreatePostCommandHandler>.Instance);
        var command = new CreatePostCommand(authorA.Id, "Alice stealing Bob image", new List<Guid> { bobMedia.Id });

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreatePost_WithUnreadyMedia_ShouldThrowValidationException()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(author);

        // Media still in uploading status
        var media = MediaEntity.Create(author.Id, "uploading.png", "posts/up.png", "image/png", 1024);
        await dbContext.Media.AddAsync(media);
        await dbContext.SaveChangesAsync();

        var handler = new CreatePostCommandHandler(dbContext, NullLogger<CreatePostCommandHandler>.Instance);
        var command = new CreatePostCommand(author.Id, "Text", new List<Guid> { media.Id });

        var act = () => handler.Handle(command, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Values.SelectMany(v => v).Should().ContainMatch("*not ready*");
    }

    #endregion

    #region GetPost & DeletePost Slice Tests

    [Fact]
    public async Task GetPost_ExistingPost_ShouldReturnPostDetails()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(author);

        var post = Post.Create(author.Id, "Hello detail!", 0);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetPostQueryHandler(dbContext, blockService, NullLogger<GetPostQueryHandler>.Instance);

        var result = await handler.Handle(new GetPostQuery(post.Id, null), CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(post.Id);
        result.Content.Should().Be("Hello detail!");
        result.Author.Username.Should().Be("alice");
    }

    [Fact]
    public async Task GetPost_SoftDeleted_ShouldThrowNotFoundException()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(author);

        var post = Post.Create(author.Id, "Deleted post", 0);
        post.Delete();
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetPostQueryHandler(dbContext, blockService, NullLogger<GetPostQueryHandler>.Instance);

        var act = () => handler.Handle(new GetPostQuery(post.Id, null), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetPost_WhenBlocked_ShouldThrowNotFoundException()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var viewer = User.Create("bob", "bob@test.com", "hash", "Bob");
        var block = Block.Create(author.Id, viewer.Id);
        await dbContext.Users.AddRangeAsync(author, viewer);
        await dbContext.Blocks.AddAsync(block);

        var post = Post.Create(author.Id, "Alice secret post", 0);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetPostQueryHandler(dbContext, blockService, NullLogger<GetPostQueryHandler>.Instance);

        var act = () => handler.Handle(new GetPostQuery(post.Id, viewer.Id), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeletePost_ByOwner_ShouldSoftDelete()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(author);

        var post = Post.Create(author.Id, "My post", 0);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var handler = new DeletePostCommandHandler(dbContext, NullLogger<DeletePostCommandHandler>.Instance);
        await handler.Handle(new DeletePostCommand(post.Id, author.Id), CancellationToken.None);

        var persisted = await dbContext.Posts.FirstOrDefaultAsync(p => p.Id == post.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeTrue();
        persisted.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeletePost_ByNonOwner_ShouldThrowForbiddenException()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("alice", "alice@test.com", "hash", "Alice");
        var otherUser = User.Create("bob", "bob@test.com", "hash", "Bob");
        await dbContext.Users.AddRangeAsync(author, otherUser);

        var post = Post.Create(author.Id, "Alice post", 0);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var handler = new DeletePostCommandHandler(dbContext, NullLogger<DeletePostCommandHandler>.Instance);
        var act = () => handler.Handle(new DeletePostCommand(post.Id, otherUser.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region CreateReply & GetReplies & DeleteReply Slice Tests

    [Fact]
    public async Task CreateReply_DirectAndNested_ShouldIncrementPostReplyCount()
    {
        using var dbContext = CreateDbContext();
        var author1 = User.Create("alice", "alice@test.com", "hash", "Alice");
        var author2 = User.Create("bob", "bob@test.com", "hash", "Bob");
        await dbContext.Users.AddRangeAsync(author1, author2);

        var post = Post.Create(author1.Id, "Top level post", 0);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var createReplyHandler = new CreateReplyCommandHandler(dbContext, blockService, NullLogger<CreateReplyCommandHandler>.Instance);

        // 1. Bob creates direct Reply A
        var replyA = await createReplyHandler.Handle(new CreateReplyCommand(post.Id, author2.Id, "Direct Reply A", null), CancellationToken.None);
        replyA.Should().NotBeNull();
        replyA.Content.Should().Be("Direct Reply A");
        replyA.ParentReplyId.Should().BeNull();

        // 2. Alice creates nested Reply A.1 under Reply A
        var replyA1 = await createReplyHandler.Handle(new CreateReplyCommand(post.Id, author1.Id, "Nested Reply A.1", replyA.Id), CancellationToken.None);
        replyA1.Should().NotBeNull();
        replyA1.ParentReplyId.Should().Be(replyA.Id);

        // Verify post counter
        var updatedPost = await dbContext.Posts.FindAsync(post.Id);
        updatedPost!.ReplyCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateReply_CrossPostParent_ShouldThrowValidationException()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(user);

        var post1 = Post.Create(user.Id, "Post 1", 0);
        var post2 = Post.Create(user.Id, "Post 2", 0);
        await dbContext.Posts.AddRangeAsync(post1, post2);
        await dbContext.SaveChangesAsync();

        var replyOnPost2 = PostReply.Create(post2.Id, user.Id, "Reply on post 2");
        await dbContext.PostReplies.AddAsync(replyOnPost2);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new CreateReplyCommandHandler(dbContext, blockService, NullLogger<CreateReplyCommandHandler>.Instance);

        // Attempt replying to Post 1 using reply on Post 2 as parent
        var act = () => handler.Handle(new CreateReplyCommand(post1.Id, user.Id, "Invalid cross-post reply", replyOnPost2.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Values.SelectMany(v => v).Should().ContainMatch("*does not belong*");
    }

    [Fact]
    public async Task GetReplies_HierarchicalOrdering_ShouldReturnParentBeforeChildTree()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(user);

        var post = Post.Create(user.Id, "Main Post", 0);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        // Create Thread:
        // Reply A (root)
        //   Reply A.1 (child of A)
        //     Reply A.1.1 (child of A.1)
        //   Reply A.2 (child of A)
        // Reply B (root)
        var replyA = PostReply.Create(post.Id, user.Id, "Reply A", null);
        await dbContext.PostReplies.AddAsync(replyA);
        await dbContext.SaveChangesAsync();

        var replyA1 = PostReply.Create(post.Id, user.Id, "Reply A.1", replyA.Id);
        await dbContext.PostReplies.AddAsync(replyA1);
        await dbContext.SaveChangesAsync();

        var replyA11 = PostReply.Create(post.Id, user.Id, "Reply A.1.1", replyA1.Id);
        await dbContext.PostReplies.AddAsync(replyA11);
        await dbContext.SaveChangesAsync();

        var replyA2 = PostReply.Create(post.Id, user.Id, "Reply A.2", replyA.Id);
        await dbContext.PostReplies.AddAsync(replyA2);
        await dbContext.SaveChangesAsync();

        var replyB = PostReply.Create(post.Id, user.Id, "Reply B", null);
        await dbContext.PostReplies.AddAsync(replyB);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetRepliesQueryHandler(dbContext, blockService, NullLogger<GetRepliesQueryHandler>.Instance);

        var result = await handler.Handle(new GetRepliesQuery(post.Id, null, null, 50), CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);

        // Verify deterministic hierarchy order: A -> A.1 -> A.1.1 -> A.2 -> B
        result.Items[0].Content.Should().Be("Reply A");
        result.Items[0].Depth.Should().Be(0);

        result.Items[1].Content.Should().Be("Reply A.1");
        result.Items[1].Depth.Should().Be(1);

        result.Items[2].Content.Should().Be("Reply A.1.1");
        result.Items[2].Depth.Should().Be(2);

        result.Items[3].Content.Should().Be("Reply A.2");
        result.Items[3].Depth.Should().Be(1);

        result.Items[4].Content.Should().Be("Reply B");
        result.Items[4].Depth.Should().Be(0);
    }

    [Fact]
    public async Task GetReplies_SoftDeletedParent_ShouldReturnPlaceholderPreservingChildren()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(user);

        var post = Post.Create(user.Id, "Main Post", 0);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var replyA = PostReply.Create(post.Id, user.Id, "Reply A", null);
        replyA.Delete(); // Soft-deleted
        await dbContext.PostReplies.AddAsync(replyA);
        await dbContext.SaveChangesAsync();

        var replyA1 = PostReply.Create(post.Id, user.Id, "Reply A.1 (Active child)", replyA.Id);
        await dbContext.PostReplies.AddAsync(replyA1);
        await dbContext.SaveChangesAsync();

        var blockService = new BlockIsolationService(dbContext);
        var handler = new GetRepliesQueryHandler(dbContext, blockService, NullLogger<GetRepliesQueryHandler>.Instance);

        var result = await handler.Handle(new GetRepliesQuery(post.Id, null, null, 50), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items[0].IsDeleted.Should().BeTrue();
        result.Items[0].Content.Should().Contain("deleted by the author");
        result.Items[1].Content.Should().Be("Reply A.1 (Active child)");
        result.Items[1].Depth.Should().Be(1);
    }

    [Fact]
    public async Task DeleteReply_ByOwner_ShouldSoftDeleteAndDecrementReplyCount()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@test.com", "hash", "Alice");
        await dbContext.Users.AddAsync(user);

        var post = Post.Create(user.Id, "Main Post", 0);
        post.IncrementReplyCount();
        await dbContext.Posts.AddAsync(post);

        var reply = PostReply.Create(post.Id, user.Id, "Reply to delete", null);
        await dbContext.PostReplies.AddAsync(reply);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteReplyCommandHandler(dbContext, NullLogger<DeleteReplyCommandHandler>.Instance);
        await handler.Handle(new DeleteReplyCommand(reply.Id, user.Id), CancellationToken.None);

        var persistedReply = await dbContext.PostReplies.FindAsync(reply.Id);
        persistedReply!.IsDeleted.Should().BeTrue();

        var updatedPost = await dbContext.Posts.FindAsync(post.Id);
        updatedPost!.ReplyCount.Should().Be(0);
    }

    #endregion
}
