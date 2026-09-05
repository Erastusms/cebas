using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class OutboxAtomicityIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OutboxAtomicityIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithCookies()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
    }

    private async Task<(HttpClient client, Guid userId, string username)> RegisterAndLoginUserAsync(string username, string email)
    {
        var client = CreateClientWithCookies();
        var registerRequest = new RegisterRequest(username, email, "Password123!", $"Display {username}");
        var regResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "Password123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meResponse = await client.GetAsync("/api/v1/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await meResponse.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);
        var userId = meBody!.Data.GetProperty("id").GetGuid();

        return (client, userId, username);
    }

    [Fact]
    public async Task CreatePost_ShouldAtomicallyPersistPostAndOutboxEvent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, userId, _) = await RegisterAndLoginUserAsync($"outbox_user_{suffix}", $"outbox_user_{suffix}@test.com");

        var createPostReq = new CreatePostRequest("Atomicity test celotehan", null);
        var postRes = await client.PostAsJsonAsync("/api/v1/posts", createPostReq);
        postRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);
        var postId = postBody!.Data.GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Verify post exists
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId);
        post.Should().NotBeNull();

        // Verify outbox event exists for POST_CREATED
        var outboxEvent = await db.OutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == postId && e.EventType == "POST_CREATED");
        outboxEvent.Should().NotBeNull();
        outboxEvent!.AggregateType.Should().Be("Post");
        outboxEvent.Payload.Should().Contain("Atomicity test celotehan");
    }

    [Fact]
    public async Task CreateLike_ShouldAtomicallyPersistLikeNotificationAndOutboxEvents()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, authorId, _) = await RegisterAndLoginUserAsync($"author_outbox_{suffix}", $"author_outbox_{suffix}@test.com");
        var (likerClient, likerId, _) = await RegisterAndLoginUserAsync($"liker_outbox_{suffix}", $"liker_outbox_{suffix}@test.com");

        var createPostReq = new CreatePostRequest("Post to be liked for outbox test", null);
        var postRes = await authorClient.PostAsJsonAsync("/api/v1/posts", createPostReq);
        postRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);
        var postId = postBody!.Data.GetProperty("id").GetGuid();

        // Liker likes the post
        var likeRes = await likerClient.PostAsync($"/api/v1/posts/{postId}/likes", null);
        likeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Verify Notification was saved
        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.RecipientId == authorId && n.ActorId == likerId);
        notif.Should().NotBeNull();
        notif!.Type.Should().Be(Domain.Entities.NotificationType.PostLiked);

        // Verify Outbox events
        var outboxLiked = await db.OutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == postId && e.EventType == "POST_LIKED");
        outboxLiked.Should().NotBeNull();

        var outboxNotif = await db.OutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == notif.Id && e.EventType == "NOTIFICATION_CREATED");
        outboxNotif.Should().NotBeNull();
    }
}
