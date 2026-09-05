using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Engagements;
using CEBAS.Application.Contracts.Notifications;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Application.Contracts.SocialGraph;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class NotificationEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public NotificationEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
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

    private async Task<Guid> CreatePostAsync(HttpClient authorClient, string content = "Notification test post")
    {
        var req = new CreatePostRequest(content, null);
        var res = await authorClient.PostAsJsonAsync("/api/v1/posts", req);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        return body!.Data!.Id;
    }

    [Fact]
    public async Task NotificationLifecycle_LikesRepliesAndFollows_ShouldCreateAndManageNotifications()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, authorId, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (actorClient, actorId, actorUsername) = await RegisterAndLoginUserAsync($"actor_{suffix}", $"actor_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Hello from author");

        // 1. Initial unread count should be 0
        var unreadRes0 = await authorClient.GetAsync("/api/v1/notifications/unread-count");
        unreadRes0.StatusCode.Should().Be(HttpStatusCode.OK);
        var unreadBody0 = await unreadRes0.Content.ReadFromJsonAsync<ApiResponse<UnreadNotificationCountResponse>>(_jsonOptions);
        unreadBody0!.Data!.UnreadCount.Should().Be(0);

        // 2. Actor likes author's post
        var likeRes = await actorClient.PostAsync($"/api/v1/posts/{postId}/likes", null);
        likeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Actor replies to author's post
        var replyReq = new CreateReplyRequest("Great post!", null);
        var replyRes = await actorClient.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", replyReq);
        if (replyRes.StatusCode != HttpStatusCode.Created)
        {
            var err = await replyRes.Content.ReadAsStringAsync();
            throw new Exception($"Reply failed with {replyRes.StatusCode}: {err}");
        }
        replyRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var replyBody = await replyRes.Content.ReadFromJsonAsync<ApiResponse<ReplyResponse>>(_jsonOptions);
        var replyId = replyBody!.Data!.Id;

        // 4. Actor follows author
        var followRes = await actorClient.PostAsync($"/api/v1/users/{authorId}/follow", null);
        followRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Author checks unread count -> should be 3
        var unreadRes1 = await authorClient.GetAsync("/api/v1/notifications/unread-count");
        unreadRes1.StatusCode.Should().Be(HttpStatusCode.OK);
        var unreadBody1 = await unreadRes1.Content.ReadFromJsonAsync<ApiResponse<UnreadNotificationCountResponse>>(_jsonOptions);
        unreadBody1!.Data!.UnreadCount.Should().Be(3);

        // 6. Author retrieves notification list
        var notifsRes = await authorClient.GetAsync("/api/v1/notifications?limit=10");
        notifsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifsBody = await notifsRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<NotificationResponseDto>>>(_jsonOptions);
        var notifications = notifsBody!.Data!.Items.ToList();

        notifications.Should().HaveCount(3);
        notifications.Should().Contain(n => n.Type == "USER_FOLLOWED" && n.Actor.Username == actorUsername);
        notifications.Should().Contain(n => n.Type == "POST_REPLIED" && n.Actor.Username == actorUsername);
        notifications.Should().Contain(n => n.Type == "POST_LIKED" && n.Actor.Username == actorUsername);
        notifications.All(n => !n.IsRead).Should().BeTrue();

        // Verify reply notification points to parent post ID so client navigation reaches the post
        var replyNotif = notifications.First(n => n.Type == "POST_REPLIED");
        replyNotif.TargetId.Should().Be(postId);
        replyNotif.TargetType.Should().Be("POST");

        // Verify that navigating to /api/v1/posts/{replyId} directly resolves to the parent post
        var resolveReplyRes = await authorClient.GetAsync($"/api/v1/posts/{replyId}");
        resolveReplyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolvedPostBody = await resolveReplyRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        resolvedPostBody!.Data!.Id.Should().Be(postId);

        var firstNotif = notifications.First();

        // 7. Actor tries to mark Author's notification as read -> 403 Forbidden
        var forbiddenRes = await actorClient.PatchAsync($"/api/v1/notifications/{firstNotif.Id}/read", null);
        forbiddenRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 8. Author marks the first notification as read
        var readRes = await authorClient.PatchAsync($"/api/v1/notifications/{firstNotif.Id}/read", null);
        readRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var readBody = await readRes.Content.ReadFromJsonAsync<ApiResponse<MarkNotificationReadResponse>>(_jsonOptions);
        readBody!.Data!.Id.Should().Be(firstNotif.Id);
        readBody.Data.IsRead.Should().BeTrue();

        // Unread count should now be 2
        var unreadRes2 = await authorClient.GetAsync("/api/v1/notifications/unread-count");
        var unreadBody2 = await unreadRes2.Content.ReadFromJsonAsync<ApiResponse<UnreadNotificationCountResponse>>(_jsonOptions);
        unreadBody2!.Data!.UnreadCount.Should().Be(2);

        // 9. Author marks all notifications as read
        var readAllRes = await authorClient.PatchAsync("/api/v1/notifications/read-all", null);
        readAllRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var readAllBody = await readAllRes.Content.ReadFromJsonAsync<ApiResponse<MarkAllNotificationsReadResponse>>(_jsonOptions);
        readAllBody!.Data!.MarkedReadCount.Should().Be(2);

        // Unread count should now be 0
        var unreadRes3 = await authorClient.GetAsync("/api/v1/notifications/unread-count");
        var unreadBody3 = await unreadRes3.Content.ReadFromJsonAsync<ApiResponse<UnreadNotificationCountResponse>>(_jsonOptions);
        unreadBody3!.Data!.UnreadCount.Should().Be(0);

        // Querying with unreadOnly=true should return empty list
        var unreadListRes = await authorClient.GetAsync("/api/v1/notifications?unreadOnly=true");
        unreadListRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var unreadListBody = await unreadListRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<NotificationResponseDto>>>(_jsonOptions);
        unreadListBody!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Notifications_ShouldBeFilteredByBidirectionalBlock()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, authorId, _) = await RegisterAndLoginUserAsync($"author_b_{suffix}", $"author_b_{suffix}@test.com");
        var (actorClient, actorId, _) = await RegisterAndLoginUserAsync($"actor_b_{suffix}", $"actor_b_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Post before block");

        // Actor likes author's post
        var likeRes = await actorClient.PostAsync($"/api/v1/posts/{postId}/likes", null);
        likeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Author verifies notification exists
        var notifsRes1 = await authorClient.GetAsync("/api/v1/notifications");
        var notifsBody1 = await notifsRes1.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<NotificationResponseDto>>>(_jsonOptions);
        notifsBody1!.Data!.Items.Should().Contain(n => n.Type == "POST_LIKED");

        // Author blocks Actor
        var blockRes = await authorClient.PostAsync($"/api/v1/users/{actorId}/block", null);
        blockRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Author retrieves notifications again -> actor's notifications are filtered out by block isolation!
        var notifsRes2 = await authorClient.GetAsync("/api/v1/notifications");
        var notifsBody2 = await notifsRes2.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<NotificationResponseDto>>>(_jsonOptions);
        notifsBody2!.Data!.Items.Should().NotContain(n => n.Actor.Id == actorId);
    }
}
