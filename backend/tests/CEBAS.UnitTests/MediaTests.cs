using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using CEBAS.Api.Features.Media.Confirm;
using CEBAS.Api.Features.Media.Upload;
using CEBAS.Api.Features.Users.UpdateAvatar;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Storage;

namespace CEBAS.UnitTests;

public class MediaTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private LocalFileStorageAdapter CreateStorageAdapter(string? testDir = null)
    {
        var dir = testDir ?? Path.Combine(Path.GetTempPath(), "cebas_test_storage_" + Guid.NewGuid().ToString("N"));
        var options = Options.Create(new MediaStorageOptions
        {
            Provider = "Local",
            RootPath = dir,
            UploadUrlExpirationMinutes = 15,
            MaxFileSizeBytes = 5 * 1024 * 1024
        });

        return new LocalFileStorageAdapter(options, NullLogger<LocalFileStorageAdapter>.Instance);
    }

    // =========================================================================
    // 1. Domain Entity & Lifecycle Tests
    // =========================================================================

    [Fact]
    public void MediaCreate_WithValidParameters_ShouldInitializeInUploadingStatus()
    {
        var userId = Guid.NewGuid();
        var media = Media.Create(userId, "avatar.png", "media/user1/123.png", "image/png", 1024);

        media.Should().NotBeNull();
        media.OwnerUserId.Should().Be(userId);
        media.OriginalFileName.Should().Be("avatar.png");
        media.StorageKey.Should().Be("media/user1/123.png");
        media.MimeType.Should().Be("image/png");
        media.FileSize.Should().Be(1024);
        media.Status.Should().Be(MediaStatus.Uploading);
        media.ConfirmedAt.Should().BeNull();
        media.DomainEvents.Should().ContainSingle(e => e is Domain.Events.MediaUploadInitiatedDomainEvent);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public void MediaCreate_WithUnsupportedMimeType_ShouldThrowValidationException(string unsupportedMime)
    {
        var act = () => Media.Create(Guid.NewGuid(), "file.ext", "media/user/1.ext", unsupportedMime, 1024);
        act.Should().Throw<ValidationException>().Where(ex => ex.Errors.ContainsKey("MimeType"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5 * 1024 * 1024 + 1)] // > 5MB
    public void MediaCreate_WithInvalidFileSize_ShouldThrowValidationException(long invalidSize)
    {
        var act = () => Media.Create(Guid.NewGuid(), "avatar.jpg", "media/user/1.jpg", "image/jpeg", invalidSize);
        act.Should().Throw<ValidationException>().Where(ex => ex.Errors.ContainsKey("FileSize"));
    }

    [Fact]
    public void MediaConfirm_WhenUploading_ShouldTransitionToReadyAndPublishEvent()
    {
        var media = Media.Create(Guid.NewGuid(), "photo.webp", "media/user/1.webp", "image/webp", 2048);
        media.ClearDomainEvents();

        media.Confirm();

        media.Status.Should().Be(MediaStatus.Ready);
        media.ConfirmedAt.Should().NotBeNull();
        media.DomainEvents.Should().ContainSingle(e => e is Domain.Events.MediaUploadConfirmedDomainEvent);
    }

    [Fact]
    public void MediaConfirm_WhenAlreadyReady_ShouldBeIdempotent()
    {
        var media = Media.Create(Guid.NewGuid(), "photo.webp", "media/user/1.webp", "image/webp", 2048);
        media.Confirm();
        var confirmedAt = media.ConfirmedAt;

        // Second confirmation
        media.Confirm();

        media.Status.Should().Be(MediaStatus.Ready);
        media.ConfirmedAt.Should().Be(confirmedAt);
    }

    // =========================================================================
    // 2. User Avatar Integration & Ownership Tests
    // =========================================================================

    [Fact]
    public void UserUpdateAvatar_WithOwnedReadyMedia_ShouldUpdateAvatarSuccessfully()
    {
        var user = User.Create("johndoe", "john@example.com", "hash", "John");
        var media = Media.Create(user.Id, "avatar.png", "media/johndoe/1.png", "image/png", 1024);
        media.Confirm();

        user.UpdateAvatar(media);

        user.AvatarMediaId.Should().Be(media.Id);
        user.AvatarUrl.Should().Be($"/api/v1/media/{media.Id}");
        user.DomainEvents.Should().Contain(e => e is Domain.Events.AvatarUpdatedDomainEvent);
    }

    [Fact]
    public void UserUpdateAvatar_WithAnotherUserMedia_ShouldThrowForbiddenException()
    {
        var user1 = User.Create("user1", "u1@example.com", "hash", "User 1");
        var user2 = User.Create("user2", "u2@example.com", "hash", "User 2");

        var mediaOfUser2 = Media.Create(user2.Id, "avatar.png", "media/user2/1.png", "image/png", 1024);
        mediaOfUser2.Confirm();

        var act = () => user1.UpdateAvatar(mediaOfUser2);
        act.Should().Throw<ForbiddenException>().WithMessage("*Cannot assign another user's media*");
    }

    [Fact]
    public void UserUpdateAvatar_WithUnconfirmedMedia_ShouldThrowValidationException()
    {
        var user = User.Create("alice", "alice@example.com", "hash", "Alice");
        var media = Media.Create(user.Id, "avatar.png", "media/alice/1.png", "image/png", 1024);
        // Media is still in UPLOADING status

        var act = () => user.UpdateAvatar(media);
        act.Should().Throw<ValidationException>().Where(ex => ex.Errors.ContainsKey("Media"));
    }

    // =========================================================================
    // 3. Storage Abstraction & Path Traversal / Magic Bytes Tests
    // =========================================================================

    [Fact]
    public void StorageAdapter_PathTraversalAttempt_ShouldThrowForbiddenException()
    {
        var adapter = CreateStorageAdapter();
        var maliciousKeys = new[]
        {
            "../../etc/passwd",
            "media/../../../secret.txt",
            "..\\..\\windows\\system32\\calc.exe",
            "media/user/../../../../root.txt"
        };

        foreach (var key in maliciousKeys)
        {
            var act = () => adapter.ResolveAndValidatePath(key);
            act.Should().Throw<ForbiddenException>();
        }
    }

    [Fact]
    public async Task StorageAdapter_SaveAndRetrieveValidImage_ShouldSucceed()
    {
        var adapter = CreateStorageAdapter();
        // Valid PNG header: 89 50 4E 47 0D 0A 1A 0A
        byte[] validPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        var storageKey = "media/testuser/avatar.png";

        using var stream = new MemoryStream(validPng);
        await adapter.SaveAsync(storageKey, stream, "image/png");

        var exists = await adapter.ExistsAsync(storageKey);
        exists.Should().BeTrue();

        var metadata = await adapter.GetMetadataAsync(storageKey);
        metadata.Should().NotBeNull();
        metadata!.ContentLength.Should().Be(validPng.Length);
        metadata.ContentType.Should().Be("image/png");

        using (var readStream = await adapter.OpenReadAsync(storageKey))
        {
            readStream.Should().NotBeNull();
            using var readMs = new MemoryStream();
            await readStream!.CopyToAsync(readMs);
            readMs.ToArray().Should().Equal(validPng);
        }

        await adapter.DeleteAsync(storageKey);
        (await adapter.ExistsAsync(storageKey)).Should().BeFalse();
    }

    [Fact]
    public async Task StorageAdapter_SaveWithCorruptedMagicBytes_ShouldThrowValidationException()
    {
        var adapter = CreateStorageAdapter();
        byte[] fakeJpeg = [0x00, 0x01, 0x02, 0x03]; // Not starting with FF D8 FF
        var storageKey = "media/testuser/fake.jpg";

        using var stream = new MemoryStream(fakeJpeg);
        var act = () => adapter.SaveAsync(storageKey, stream, "image/jpeg");
        await act.Should().ThrowAsync<ValidationException>().Where(ex => ex.Errors.ContainsKey("File"));
    }

    // =========================================================================
    // 4. CQRS Command Handler Tests
    // =========================================================================

    [Fact]
    public async Task CreateMediaUploadCommandHandler_ValidRequest_ShouldCreateRecordAndReturnTarget()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("johndoe", "john@example.com", "hash", "John Doe");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var storageAdapter = CreateStorageAdapter();
        var options = Options.Create(new MediaStorageOptions { RootPath = "Storage/Media", UploadUrlExpirationMinutes = 15 });
        var handler = new CreateMediaUploadCommandHandler(dbContext, storageAdapter, options, NullLogger<CreateMediaUploadCommandHandler>.Instance);

        var command = new CreateMediaUploadCommand(user.Id, "profile.webp", "image/webp", 2048);
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.MediaId.Should().NotBeEmpty();
        result.UploadUrl.Should().Contain("api/v1/media/upload");
        result.Method.Should().Be("PUT");

        var persistedMedia = await dbContext.Media.FindAsync(result.MediaId);
        persistedMedia.Should().NotBeNull();
        persistedMedia!.Status.Should().Be(MediaStatus.Uploading);
        persistedMedia.OwnerUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task ConfirmMediaUploadCommandHandler_ValidUpload_ShouldTransitionToReady()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@example.com", "hash", "Alice");
        await dbContext.Users.AddAsync(user);

        var storageAdapter = CreateStorageAdapter();
        var storageKey = "media/alice/test.png";
        byte[] validPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        using var ms = new MemoryStream(validPng);
        await storageAdapter.SaveAsync(storageKey, ms, "image/png");

        var media = Media.Create(user.Id, "test.png", storageKey, "image/png", validPng.Length);
        await dbContext.Media.AddAsync(media);
        await dbContext.SaveChangesAsync();

        var handler = new ConfirmMediaUploadCommandHandler(dbContext, storageAdapter, NullLogger<ConfirmMediaUploadCommandHandler>.Instance);
        var result = await handler.Handle(new ConfirmMediaUploadCommand(user.Id, media.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("READY");

        var updated = await dbContext.Media.FindAsync(media.Id);
        updated!.Status.Should().Be(MediaStatus.Ready);
    }

    [Fact]
    public async Task ConfirmMediaUploadCommandHandler_AnotherUser_ShouldThrowForbiddenException()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("user1", "u1@example.com", "hash", "User 1");
        var user2 = User.Create("user2", "u2@example.com", "hash", "User 2");
        await dbContext.Users.AddRangeAsync(user1, user2);

        var media = Media.Create(user1.Id, "test.png", "media/user1/test.png", "image/png", 100);
        await dbContext.Media.AddAsync(media);
        await dbContext.SaveChangesAsync();

        var storageAdapter = CreateStorageAdapter();
        var handler = new ConfirmMediaUploadCommandHandler(dbContext, storageAdapter, NullLogger<ConfirmMediaUploadCommandHandler>.Instance);

        var act = () => handler.Handle(new ConfirmMediaUploadCommand(user2.Id, media.Id), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Cannot confirm media owned by another user*");
    }

    [Fact]
    public async Task UpdateAvatarCommandHandler_ValidReadyMedia_ShouldUpdateUserAvatar()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("bob", "bob@example.com", "hash", "Bob");
        var media = Media.Create(user.Id, "bob.jpg", "media/bob/bob.jpg", "image/jpeg", 500);
        media.Confirm();

        await dbContext.Users.AddAsync(user);
        await dbContext.Media.AddAsync(media);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateAvatarCommandHandler(dbContext, NullLogger<UpdateAvatarCommandHandler>.Instance);
        var result = await handler.Handle(new UpdateAvatarCommand(user.Id, media.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.AvatarUrl.Should().Be($"/api/v1/media/{media.Id}");

        var updatedUser = await dbContext.Users.FindAsync(user.Id);
        updatedUser!.AvatarMediaId.Should().Be(media.Id);
    }
}
