using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Engagements;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Application.Contracts.SocialGraph;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class EngagementEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EngagementEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
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

    private async Task<Guid> CreatePostAsync(HttpClient authorClient, string content = "Engagement test post")
    {
        var req = new CreatePostRequest(content, null);
        var res = await authorClient.PostAsJsonAsync("/api/v1/posts", req);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        return body!.Data!.Id;
    }

    #region Like Endpoint Tests

    [Fact]
    public async Task LikeAndUnlike_Lifecycle_ShouldSucceedWithAuthoritativeCounters()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (likerClient, _, _) = await RegisterAndLoginUserAsync($"liker_{suffix}", $"liker_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Testing likes");

        // 1. POST Like
        var likeRes = await likerClient.PostAsync($"/api/v1/posts/{postId}/likes", null);
        likeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var likeBody = await likeRes.Content.ReadFromJsonAsync<ApiResponse<LikeResponse>>(_jsonOptions);
        likeBody!.Data!.Liked.Should().BeTrue();
        likeBody.Data.LikeCount.Should().Be(1);

        // Verify post detail reflects like
        var postRes = await likerClient.GetAsync($"/api/v1/posts/{postId}");
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        postBody!.Data!.Liked.Should().BeTrue();
        postBody.Data.LikeCount.Should().Be(1);

        // 2. DELETE Like
        var unlikeRes = await likerClient.DeleteAsync($"/api/v1/posts/{postId}/likes");
        unlikeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var unlikeBody = await unlikeRes.Content.ReadFromJsonAsync<ApiResponse<LikeResponse>>(_jsonOptions);
        unlikeBody!.Data!.Liked.Should().BeFalse();
        unlikeBody.Data.LikeCount.Should().Be(0);

        // Verify post detail reflects unlike
        var postRes2 = await likerClient.GetAsync($"/api/v1/posts/{postId}");
        var postBody2 = await postRes2.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        postBody2!.Data!.Liked.Should().BeFalse();
        postBody2.Data.LikeCount.Should().Be(0);
    }

    [Fact]
    public async Task Like_IdempotentDuplicateRequests_ShouldMaintainCorrectState()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (likerClient, _, _) = await RegisterAndLoginUserAsync($"liker_{suffix}", $"liker_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Idempotent likes test");

        // First Like
        var res1 = await likerClient.PostAsync($"/api/v1/posts/{postId}/likes", null);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second Like (Duplicate)
        var res2 = await likerClient.PostAsync($"/api/v1/posts/{postId}/likes", null);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<ApiResponse<LikeResponse>>(_jsonOptions);
        body2!.Data!.Liked.Should().BeTrue();
        body2.Data.LikeCount.Should().Be(1);

        // Third Like (Duplicate)
        var res3 = await likerClient.PostAsync($"/api/v1/posts/{postId}/likes", null);
        res3.StatusCode.Should().Be(HttpStatusCode.OK);
        var body3 = await res3.Content.ReadFromJsonAsync<ApiResponse<LikeResponse>>(_jsonOptions);
        body3!.Data!.Liked.Should().BeTrue();
        body3.Data.LikeCount.Should().Be(1);
    }

    [Fact]
    public async Task Unlike_IdempotentDuplicateRequests_ShouldNeverMakeCounterNegative()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (likerClient, _, _) = await RegisterAndLoginUserAsync($"liker_{suffix}", $"liker_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Idempotent unlike test");

        // Unlike when never liked
        var res1 = await likerClient.DeleteAsync($"/api/v1/posts/{postId}/likes");
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var body1 = await res1.Content.ReadFromJsonAsync<ApiResponse<LikeResponse>>(_jsonOptions);
        body1!.Data!.Liked.Should().BeFalse();
        body1.Data.LikeCount.Should().Be(0);

        // Second unlike
        var res2 = await likerClient.DeleteAsync($"/api/v1/posts/{postId}/likes");
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<ApiResponse<LikeResponse>>(_jsonOptions);
        body2!.Data!.Liked.Should().BeFalse();
        body2.Data.LikeCount.Should().Be(0);
    }

    [Fact]
    public async Task Like_ConcurrentRequests_ShouldProduceExactlyOneLikeAndOneCount()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (likerClient, _, _) = await RegisterAndLoginUserAsync($"liker_{suffix}", $"liker_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Concurrent likes test");

        // Fire 10 simultaneous POST /likes requests from the same user
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => likerClient.PostAsync($"/api/v1/posts/{postId}/likes", null))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        foreach (var r in responses)
        {
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Authoritative final state check
        var postRes = await likerClient.GetAsync($"/api/v1/posts/{postId}");
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        postBody!.Data!.Liked.Should().BeTrue();
        postBody.Data.LikeCount.Should().Be(1);
    }

    [Fact]
    public async Task Unlike_ConcurrentRequests_ShouldSafelyReachZeroCount()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (likerClient, _, _) = await RegisterAndLoginUserAsync($"liker_{suffix}", $"liker_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Concurrent unlikes test");

        // Initial like
        await likerClient.PostAsync($"/api/v1/posts/{postId}/likes", null);

        // Fire 10 simultaneous DELETE /likes requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => likerClient.DeleteAsync($"/api/v1/posts/{postId}/likes"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        foreach (var r in responses)
        {
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var postRes = await likerClient.GetAsync($"/api/v1/posts/{postId}");
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        postBody!.Data!.Liked.Should().BeFalse();
        postBody.Data.LikeCount.Should().Be(0);
    }

    #endregion

    #region Bookmark Endpoint Tests

    [Fact]
    public async Task BookmarkAndUnbookmark_Lifecycle_ShouldSucceed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (userClient, _, _) = await RegisterAndLoginUserAsync($"user_{suffix}", $"user_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Testing bookmarks");

        // 1. POST Bookmark
        var res1 = await userClient.PostAsync($"/api/v1/posts/{postId}/bookmarks", null);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var body1 = await res1.Content.ReadFromJsonAsync<ApiResponse<BookmarkResponse>>(_jsonOptions);
        body1!.Data!.Bookmarked.Should().BeTrue();
        body1.Data.BookmarkCount.Should().Be(1);

        // 2. DELETE Bookmark
        var res2 = await userClient.DeleteAsync($"/api/v1/posts/{postId}/bookmarks");
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<ApiResponse<BookmarkResponse>>(_jsonOptions);
        body2!.Data!.Bookmarked.Should().BeFalse();
        body2.Data.BookmarkCount.Should().Be(0);
    }

    [Fact]
    public async Task Bookmark_ConcurrentRequests_ShouldProduceExactlyOneRecord()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (userClient, _, _) = await RegisterAndLoginUserAsync($"user_{suffix}", $"user_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Concurrent bookmarks test");

        // Fire 10 simultaneous POST /bookmarks requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => userClient.PostAsync($"/api/v1/posts/{postId}/bookmarks", null))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        foreach (var r in responses)
        {
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var postRes = await userClient.GetAsync($"/api/v1/posts/{postId}");
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        postBody!.Data!.Bookmarked.Should().BeTrue();
        postBody.Data.BookmarkCount.Should().Be(1);
    }

    [Fact]
    public async Task GetBookmarks_ShouldReturnUserBookmarks_WithCursorPagination()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (userClient, _, _) = await RegisterAndLoginUserAsync($"user_{suffix}", $"user_{suffix}@test.com");

        var p1 = await CreatePostAsync(authorClient, "Bookmark 1");
        var p2 = await CreatePostAsync(authorClient, "Bookmark 2");
        var p3 = await CreatePostAsync(authorClient, "Bookmark 3");

        await userClient.PostAsync($"/api/v1/posts/{p1}/bookmarks", null);
        await Task.Delay(50);
        await userClient.PostAsync($"/api/v1/posts/{p2}/bookmarks", null);
        await Task.Delay(50);
        await userClient.PostAsync($"/api/v1/posts/{p3}/bookmarks", null);

        // Fetch page 1 (limit 2)
        var page1Res = await userClient.GetAsync("/api/v1/bookmarks?limit=2");
        page1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1Body = await page1Res.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<BookmarkedPostResponse>>>(_jsonOptions);

        page1Body!.Data!.Items.Should().HaveCount(2);
        page1Body.Data.HasNextPage.Should().BeTrue();
        page1Body.Data.NextCursor.Should().NotBeNullOrEmpty();

        // Fetch page 2
        var page2Res = await userClient.GetAsync($"/api/v1/bookmarks?limit=2&cursor={page1Body.Data.NextCursor}");
        page2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2Body = await page2Res.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<BookmarkedPostResponse>>>(_jsonOptions);

        page2Body!.Data!.Items.Should().HaveCount(1);
        page2Body.Data.HasNextPage.Should().BeFalse();

        var allIds = page1Body.Data.Items.Select(x => x.PostId).Concat(page2Body.Data.Items.Select(x => x.PostId)).ToList();
        allIds.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task EngagementEndpoints_WithoutAuth_ShouldReturn401()
    {
        var anonymousClient = CreateClientWithCookies();
        var fakeId = Guid.NewGuid();

        var res1 = await anonymousClient.PostAsync($"/api/v1/posts/{fakeId}/likes", null);
        res1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res2 = await anonymousClient.DeleteAsync($"/api/v1/posts/{fakeId}/likes");
        res2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res3 = await anonymousClient.PostAsync($"/api/v1/posts/{fakeId}/bookmarks", null);
        res3.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res4 = await anonymousClient.DeleteAsync($"/api/v1/posts/{fakeId}/bookmarks");
        res4.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res5 = await anonymousClient.GetAsync("/api/v1/bookmarks");
        res5.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EngagementEndpoints_WhenBlocked_ShouldReturn403Forbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, authorId, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (userClient, userId, _) = await RegisterAndLoginUserAsync($"blocked_{suffix}", $"blocked_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Post before block");

        // Author blocks the user
        var blockRes = await authorClient.PostAsync($"/api/v1/users/{userId}/block", null);
        blockRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Blocked user tries to like
        var likeRes = await userClient.PostAsync($"/api/v1/posts/{postId}/likes", null);
        likeRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Blocked user tries to bookmark
        var bookmarkRes = await userClient.PostAsync($"/api/v1/posts/{postId}/bookmarks", null);
        bookmarkRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Final Spec Validation Scenario (Section 40)

    [Fact]
    public async Task FinalValidationScenario_ConcurrentLikesAndBookmarks_ShouldBehaveExactlyAsSpecified()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (userClient, _, _) = await RegisterAndLoginUserAsync($"user_a_{suffix}", $"user_a_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Section 40 validation post");

        // Initial check
        var initialRes = await userClient.GetAsync($"/api/v1/posts/{postId}");
        var initialBody = await initialRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        initialBody!.Data!.Liked.Should().BeFalse();
        initialBody.Data.Bookmarked.Should().BeFalse();
        var initialLikeCount = initialBody.Data.LikeCount;
        var initialBookmarkCount = initialBody.Data.BookmarkCount;

        // Step 1: Execute concurrently 10 x POST /likes + 10 x POST /bookmarks
        var likeTasks = Enumerable.Range(0, 10).Select(_ => userClient.PostAsync($"/api/v1/posts/{postId}/likes", null));
        var bookmarkTasks = Enumerable.Range(0, 10).Select(_ => userClient.PostAsync($"/api/v1/posts/{postId}/bookmarks", null));

        await Task.WhenAll(likeTasks.Concat(bookmarkTasks));

        // Verify state after concurrent addition
        var afterAddRes = await userClient.GetAsync($"/api/v1/posts/{postId}");
        var afterAddBody = await afterAddRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        afterAddBody!.Data!.Liked.Should().BeTrue();
        afterAddBody.Data.LikeCount.Should().Be(initialLikeCount + 1);
        afterAddBody.Data.Bookmarked.Should().BeTrue();
        afterAddBody.Data.BookmarkCount.Should().Be(initialBookmarkCount + 1);

        // Step 2: Execute concurrently 10 x DELETE /likes + 10 x DELETE /bookmarks
        var deleteLikeTasks = Enumerable.Range(0, 10).Select(_ => userClient.DeleteAsync($"/api/v1/posts/{postId}/likes"));
        var deleteBookmarkTasks = Enumerable.Range(0, 10).Select(_ => userClient.DeleteAsync($"/api/v1/posts/{postId}/bookmarks"));

        await Task.WhenAll(deleteLikeTasks.Concat(deleteBookmarkTasks));

        // Verify state after concurrent removal
        var afterDeleteRes = await userClient.GetAsync($"/api/v1/posts/{postId}");
        var afterDeleteBody = await afterDeleteRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        afterDeleteBody!.Data!.Liked.Should().BeFalse();
        afterDeleteBody.Data.LikeCount.Should().Be(initialLikeCount);
        afterDeleteBody.Data.Bookmarked.Should().BeFalse();
        afterDeleteBody.Data.BookmarkCount.Should().Be(initialBookmarkCount);
    }

    #endregion
}
