using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Media;
using CEBAS.Application.Contracts.Users;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class MediaEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MediaEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient(string username = "johndoe", string password = "Password123!")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginRes = client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, password)).Result;
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task CreateUploadUrl_WithoutAuthentication_ShouldReturn401Unauthorized()
    {
        var unauthClient = _factory.CreateClient();
        var request = new CreateMediaUploadRequest("avatar.png", "image/png", 1024);

        var response = await unauthClient.PostAsJsonAsync("/api/v1/media/upload-url", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("file.gif", "image/gif", 1024)]
    [InlineData("file.svg", "image/svg+xml", 1024)]
    [InlineData("file.pdf", "application/pdf", 1024)]
    public async Task CreateUploadUrl_WithUnsupportedMimeType_ShouldReturn400BadRequest(string fileName, string mime, long size)
    {
        var client = CreateAuthenticatedClient();
        var request = new CreateMediaUploadRequest(fileName, mime, size);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload-url", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(_jsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeNull();
        problem.Errors!.Keys.Should().Contain("ContentType");
    }

    [Fact]
    public async Task CreateUploadUrl_WithFileSizeExceeding5MB_ShouldReturn400BadRequest()
    {
        var client = CreateAuthenticatedClient();
        var request = new CreateMediaUploadRequest("big.jpg", "image/jpeg", 6 * 1024 * 1024);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload-url", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(_jsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeNull();
        problem.Errors!.Keys.Should().Contain("FileSize");
    }

    [Fact]
    public async Task CompleteMediaUploadAndAvatarWorkflow_ShouldSucceedEndToEnd()
    {
        var client = CreateAuthenticatedClient("johndoe", "Password123!");

        // 1. Request upload target
        var prepRequest = new CreateMediaUploadRequest("test_avatar.png", "image/png", 12);
        var prepRes = await client.PostAsJsonAsync("/api/v1/media/upload-url", prepRequest);
        prepRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var prepBody = await prepRes.Content.ReadFromJsonAsync<ApiResponse<CreateMediaUploadResponse>>(_jsonOptions);
        prepBody.Should().NotBeNull();
        prepBody!.Data.Should().NotBeNull();
        var mediaId = prepBody.Data!.MediaId;
        var uploadUrl = prepBody.Data.UploadUrl;

        // 2. Direct binary upload (PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A + extra bytes)
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x01];
        using var content = new ByteArrayContent(pngBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var uploadRes = await client.PutAsync(uploadUrl, content);
        uploadRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Confirm upload
        var confirmRes = await client.PostAsync($"/api/v1/media/{mediaId}/confirm", null);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmBody = await confirmRes.Content.ReadFromJsonAsync<ApiResponse<MediaResponse>>(_jsonOptions);
        confirmBody.Should().NotBeNull();
        confirmBody!.Data!.Status.Should().Be("READY");
        confirmBody.Data.ConfirmedAt.Should().NotBeNull();

        // 4. Test idempotency of confirmation
        var reconfirmRes = await client.PostAsync($"/api/v1/media/{mediaId}/confirm", null);
        reconfirmRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Update user avatar
        var avatarRes = await client.PutAsJsonAsync("/api/v1/users/me/avatar", new UpdateAvatarRequest(mediaId));
        avatarRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var avatarBody = await avatarRes.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);
        avatarBody.Should().NotBeNull();
        avatarBody!.Data!.AvatarUrl.Should().Be($"/api/v1/media/{mediaId}");

        // 6. Verify GET /api/v1/media/{id} retrieves the binary stream
        var mediaGetRes = await client.GetAsync($"/api/v1/media/{mediaId}");
        mediaGetRes.StatusCode.Should().Be(HttpStatusCode.OK);
        mediaGetRes.Content.Headers.ContentType?.MediaType.Should().Be("image/png");

        var retrievedBytes = await mediaGetRes.Content.ReadAsByteArrayAsync();
        retrievedBytes.Should().Equal(pngBytes);
    }

    [Fact]
    public async Task ConfirmMedia_OwnedByAnotherUser_ShouldReturn403Forbidden()
    {
        var clientUser1 = CreateAuthenticatedClient("johndoe", "Password123!");
        var clientUser2 = CreateAuthenticatedClient("janedoe", "Password123!");

        // User1 initiates upload
        var prepRequest = new CreateMediaUploadRequest("avatar.webp", "image/webp", 100);
        var prepRes = await clientUser1.PostAsJsonAsync("/api/v1/media/upload-url", prepRequest);
        var prepBody = await prepRes.Content.ReadFromJsonAsync<ApiResponse<CreateMediaUploadResponse>>(_jsonOptions);
        var mediaId = prepBody!.Data!.MediaId;

        // User2 attempts to confirm User1's media
        var confirmRes = await clientUser2.PostAsync($"/api/v1/media/{mediaId}/confirm", null);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAvatar_UsingAnotherUserMedia_ShouldReturn403Forbidden()
    {
        var clientUser1 = CreateAuthenticatedClient("johndoe", "Password123!");
        var clientUser2 = CreateAuthenticatedClient("janedoe", "Password123!");

        // User1 creates and confirms media
        var prepRequest = new CreateMediaUploadRequest("avatar.png", "image/png", 12);
        var prepRes = await clientUser1.PostAsJsonAsync("/api/v1/media/upload-url", prepRequest);
        var prepBody = await prepRes.Content.ReadFromJsonAsync<ApiResponse<CreateMediaUploadResponse>>(_jsonOptions);
        var mediaId = prepBody!.Data!.MediaId;

        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x01];
        using var content = new ByteArrayContent(pngBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        await clientUser1.PutAsync(prepBody.Data.UploadUrl, content);
        await clientUser1.PostAsync($"/api/v1/media/{mediaId}/confirm", null);

        // User2 attempts to assign User1's media as their avatar
        var avatarRes = await clientUser2.PutAsJsonAsync("/api/v1/users/me/avatar", new UpdateAvatarRequest(mediaId));
        avatarRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
