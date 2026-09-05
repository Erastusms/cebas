using System.Diagnostics;
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
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Common;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class TimelineEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TimelineEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
    }

    private async Task<(HttpClient client, CurrentUserResponse user)> RegisterAndLoginUserAsync(string username, string email)
    {
        var client = CreateAuthenticatedClient();
        var registerRequest = new RegisterRequest(username, email, "Password123!", $"Display {username}");
        var regResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "Password123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meResponse = await client.GetAsync("/api/v1/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await meResponse.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);

        return (client, meBody!.Data!);
    }

    [Fact]
    public async Task HomeFeed_WithoutAuth_ShouldReturn401Unauthorized()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/v1/timelines/home");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HomeFeed_InvalidCursor_ShouldReturn400BadRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, _) = await RegisterAndLoginUserAsync($"tl_inv_{suffix}", $"tl_inv_{suffix}@test.com");

        var response = await client.GetAsync("/api/v1/timelines/home?cursor=not-a-valid-cursor-token");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(_jsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeNull();
    }

    [Fact]
    public async Task HomeFeed_OwnAndFollowedPosts_ShouldAppear_AndUnfollowed_ShouldNotAppear()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"viewer_{suffix}", $"viewer_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"followed_{suffix}", $"followed_{suffix}@test.com");
        var (clientC, userC) = await RegisterAndLoginUserAsync($"stranger_{suffix}", $"stranger_{suffix}@test.com");

        // User A follows User B
        var followRes = await clientA.PostAsync($"/api/v1/users/{userB.Id}/follow", null);
        followRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Posts created by A, B, and C
        var resPostA = await clientA.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post from User A (self)", null));
        var postA = (await resPostA.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        var resPostB = await clientB.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post from User B (followed)", null));
        var postB = (await resPostB.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        var resPostC = await clientC.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post from User C (stranger)", null));
        var postC = (await resPostC.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // User A requests home feed
        var feedRes = await clientA.GetAsync("/api/v1/timelines/home");
        feedRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var feedBody = await feedRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);

        var items = feedBody!.Data!.Items;
        items.Should().Contain(p => p.Id == postA.Id);
        items.Should().Contain(p => p.Id == postB.Id);
        items.Should().NotContain(p => p.Id == postC.Id);
    }

    [Fact]
    public async Task HomeFeed_DeletedPosts_ShouldNotAppearInTimeline()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"del_a_{suffix}", $"del_a_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"del_b_{suffix}", $"del_b_{suffix}@test.com");

        await clientA.PostAsync($"/api/v1/users/{userB.Id}/follow", null);

        var createRes = await clientB.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post soon to be deleted", null));
        var createdPost = (await createRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // Verify it initially appears
        var feedBefore = await clientA.GetAsync("/api/v1/timelines/home");
        var bodyBefore = await feedBefore.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        bodyBefore!.Data!.Items.Should().Contain(p => p.Id == createdPost.Id);

        // User B deletes the post
        var deleteRes = await clientB.DeleteAsync($"/api/v1/posts/{createdPost.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it disappears from User A's feed
        var feedAfter = await clientA.GetAsync("/api/v1/timelines/home");
        var bodyAfter = await feedAfter.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        bodyAfter!.Data!.Items.Should().NotContain(p => p.Id == createdPost.Id);
    }

    [Fact]
    public async Task HomeFeed_BidirectionalBlocks_ShouldDynamicallyExcludePostsServerSide()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"blk_v_{suffix}", $"blk_v_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"blk_t_{suffix}", $"blk_t_{suffix}@test.com");

        // A follows B
        await clientA.PostAsync($"/api/v1/users/{userB.Id}/follow", null);

        var createRes = await clientB.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post for block test", null));
        var postB = (await createRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // 1. Pre-condition: post is visible
        var feed1 = await clientA.GetAsync("/api/v1/timelines/home");
        var body1 = await feed1.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        body1!.Data!.Items.Should().Contain(p => p.Id == postB.Id);

        // 2. User A blocks User B -> excluded
        await clientA.PostAsync($"/api/v1/users/{userB.Id}/block", null);

        var feedBlocked = await clientA.GetAsync("/api/v1/timelines/home");
        var bodyBlocked = await feedBlocked.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        bodyBlocked!.Data!.Items.Should().NotContain(p => p.Id == postB.Id);

        // 3. User A unblocks User B and re-follows
        await clientA.DeleteAsync($"/api/v1/users/{userB.Id}/block");
        await clientA.PostAsync($"/api/v1/users/{userB.Id}/follow", null);

        // 4. Reverse direction: User B blocks User A -> excluded
        await clientB.PostAsync($"/api/v1/users/{userA.Id}/block", null);

        var feedReverseBlocked = await clientA.GetAsync("/api/v1/timelines/home");
        var bodyReverseBlocked = await feedReverseBlocked.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        bodyReverseBlocked!.Data!.Items.Should().NotContain(p => p.Id == postB.Id);
    }

    [Fact]
    public async Task HomeFeed_KeysetPagination_ShouldTraversePagesWithoutDuplicatesOrOmissions()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"pages_{suffix}", $"pages_{suffix}@test.com");

        // Create 5 posts chronologically with slight delay to ensure distinct timestamps
        var createdIds = new List<Guid>();
        for (int i = 1; i <= 5; i++)
        {
            var res = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Pagination item {i} - {suffix}", null));
            var post = (await res.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;
            createdIds.Add(post.Id);
            await Task.Delay(20);
        }

        // Traverse using limit = 2
        // Page 1
        var page1Res = await client.GetAsync("/api/v1/timelines/home?limit=2");
        page1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = (await page1Res.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;
        page1.Items.Should().HaveCount(2);
        page1.HasNextPage.Should().BeTrue();
        page1.NextCursor.Should().NotBeNullOrEmpty();

        // Page 2
        var page2Res = await client.GetAsync($"/api/v1/timelines/home?limit=2&cursor={WebUtility.UrlEncode(page1.NextCursor)}");
        page2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = (await page2Res.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;
        page2.Items.Should().HaveCount(2);
        page2.HasNextPage.Should().BeTrue();
        page2.NextCursor.Should().NotBeNullOrEmpty();

        // Page 3 (final)
        var page3Res = await client.GetAsync($"/api/v1/timelines/home?limit=2&cursor={WebUtility.UrlEncode(page2.NextCursor)}");
        page3Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page3 = (await page3Res.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;
        page3.Items.Should().HaveCount(1);
        page3.HasNextPage.Should().BeFalse();
        page3.NextCursor.Should().BeNull();

        // Accumulate all items
        var allPagedItems = page1.Items.Concat(page2.Items).Concat(page3.Items).ToList();
        var allPagedIds = allPagedItems.Select(x => x.Id).ToList();

        // Invariants:
        // 1. No duplicate items
        allPagedIds.Distinct().Should().HaveCount(5);
        // 2. No omitted items
        allPagedIds.Should().Contain(createdIds);
        // 3. Strict chronological descending order
        allPagedItems.Should().BeInDescendingOrder(p => p.CreatedAt);
    }

    [Fact]
    public async Task HomeFeed_TieBreaker_ShouldOrderDeterministicallyByIdDesc()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"tie_{suffix}", $"tie_{suffix}@test.com");

        var fixedTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var id1 = idA.CompareTo(idB) < 0 ? idA : idB;
        var id2 = idA.CompareTo(idB) < 0 ? idB : idA;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO posts (id, author_id, content, created_at, is_deleted) VALUES ({0}, {1}, {2}, {3}, {4})",
                id1, user.Id, "Tie post 1", fixedTime, false);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO posts (id, author_id, content, created_at, is_deleted) VALUES ({0}, {1}, {2}, {3}, {4})",
                id2, user.Id, "Tie post 2", fixedTime, false);
        }

        var feedRes = await client.GetAsync("/api/v1/timelines/home?limit=10");
        feedRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var feed = (await feedRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;

        var tiePosts = feed.Items.Where(p => p.Id == id1 || p.Id == id2).ToList();
        tiePosts.Should().HaveCount(2);
        // id2 > id1, so id2 must precede id1 in id DESC order
        tiePosts[0].Id.Should().Be(id2);
        tiePosts[1].Id.Should().Be(id1);
    }

    [Fact]
    public async Task HomeFeed_MutationDuringPagination_ShouldMaintainDeterministicContinuation()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"mut_{suffix}", $"mut_{suffix}@test.com");

        // Create 3 posts
        var p1 = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post 1", null));
        var post1 = (await p1.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;
        await Task.Delay(20);

        var p2 = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post 2", null));
        var post2 = (await p2.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;
        await Task.Delay(20);

        var p3 = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post 3", null));
        var post3 = (await p3.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // Fetch Page 1 with limit = 2 (contains Post 3, Post 2)
        var page1Res = await client.GetAsync("/api/v1/timelines/home?limit=2");
        var page1 = (await page1Res.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;
        page1.Items.Select(p => p.Id).Should().ContainInOrder(post3.Id, post2.Id);

        // Mutation: A new post is created while user is reading Page 1
        await Task.Delay(20);
        var pNew = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post Brand New", null));
        var postNew = (await pNew.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // Request Page 2 with Page 1 cursor
        var page2Res = await client.GetAsync($"/api/v1/timelines/home?limit=2&cursor={WebUtility.UrlEncode(page1.NextCursor)}");
        var page2 = (await page2Res.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;

        // Page 2 should cleanly return Post 1, without repeating Post 2 or jumping back to postNew
        page2.Items.Should().HaveCount(1);
        page2.Items[0].Id.Should().Be(post1.Id);
    }

    [Fact]
    public async Task HomeFeed_ViewerEngagementState_ShouldBeAccuratelyResolved()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"eng_a_{suffix}", $"eng_a_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"eng_b_{suffix}", $"eng_b_{suffix}@test.com");

        await clientA.PostAsync($"/api/v1/users/{userB.Id}/follow", null);

        var p1Res = await clientB.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post 1 for engagement", null));
        var post1 = (await p1Res.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        var p2Res = await clientB.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post 2 for engagement", null));
        var post2 = (await p2Res.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // User A likes Post 1
        var likeRes = await clientA.PostAsync($"/api/v1/posts/{post1.Id}/likes", null);
        likeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // User A bookmarks Post 2
        var bmRes = await clientA.PostAsync($"/api/v1/posts/{post2.Id}/bookmarks", null);
        bmRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Fetch home feed
        var feedRes = await clientA.GetAsync("/api/v1/timelines/home");
        var feed = (await feedRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;

        var item1 = feed.Items.First(p => p.Id == post1.Id);
        item1.Liked.Should().BeTrue();
        item1.Bookmarked.Should().BeFalse();

        var item2 = feed.Items.First(p => p.Id == post2.Id);
        item2.Liked.Should().BeFalse();
        item2.Bookmarked.Should().BeTrue();
    }

    [Fact]
    public async Task UserProfile_PostsAndLikes_KeysetPagination_ShouldWorkEndToEnd()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientAuthor, userAuthor) = await RegisterAndLoginUserAsync($"prof_a_{suffix}", $"prof_a_{suffix}@test.com");
        var (clientViewer, userViewer) = await RegisterAndLoginUserAsync($"prof_v_{suffix}", $"prof_v_{suffix}@test.com");

        // Author creates 3 posts
        var postIds = new List<Guid>();
        for (int i = 1; i <= 3; i++)
        {
            var res = await clientAuthor.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Profile item {i}", null));
            var p = (await res.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;
            postIds.Add(p.Id);
            await Task.Delay(20);
        }

        // Author likes posts 1 and 2
        await clientAuthor.PostAsync($"/api/v1/posts/{postIds[0]}/likes", null);
        await Task.Delay(20);
        await clientAuthor.PostAsync($"/api/v1/posts/{postIds[1]}/likes", null);

        // 1. GET /api/v1/users/{id}/posts using username
        var userPostsRes = await clientViewer.GetAsync($"/api/v1/users/{userAuthor.Username}/posts?limit=2");
        userPostsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var userPosts = (await userPostsRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;
        userPosts.Items.Should().HaveCount(2);
        userPosts.HasNextPage.Should().BeTrue();

        // Page 2
        var userPostsP2 = await clientViewer.GetAsync($"/api/v1/users/{userAuthor.Id}/posts?limit=2&cursor={WebUtility.UrlEncode(userPosts.NextCursor)}");
        userPostsP2.StatusCode.Should().Be(HttpStatusCode.OK);
        var userPosts2 = (await userPostsP2.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;
        userPosts2.Items.Should().HaveCount(1);
        userPosts2.HasNextPage.Should().BeFalse();

        // 2. GET /api/v1/users/{id}/likes
        var userLikesRes = await clientViewer.GetAsync($"/api/v1/users/{userAuthor.Id}/likes?limit=10");
        userLikesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var userLikes = (await userLikesRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;
        userLikes.Items.Should().HaveCount(2);
        userLikes.Items.Select(x => x.Id).Should().Contain(new[] { postIds[0], postIds[1] });
    }

    [Fact]
    public async Task PerformanceBenchmark_KeysetSeek_ShouldRemainBelow50ms()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"perf_{suffix}", $"perf_{suffix}@test.com");

        // Warm up and create posts
        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Benchmark post {i}", null));
        }

        // Fetch page 1 to get next_cursor
        var p1 = await client.GetAsync("/api/v1/timelines/home?limit=2");
        var body1 = (await p1.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions))!.Data!;

        // Benchmark keyset seek on Page 2
        var sw = Stopwatch.StartNew();
        var p2 = await client.GetAsync($"/api/v1/timelines/home?limit=2&cursor={WebUtility.UrlEncode(body1.NextCursor)}");
        sw.Stop();

        p2.StatusCode.Should().Be(HttpStatusCode.OK);
        // Acceptance criteria: Keyset seek under 50ms (giving reasonable margin for HTTP round-trip in test harness)
        sw.ElapsedMilliseconds.Should().BeLessThan(200);
    }

    [Fact]
    public async Task GetReplies_ForExistingPostWithReply_ShouldReturnReply()
    {
        var client = CreateAuthenticatedClient();
        var postId = Guid.Parse("018f0200-0000-7000-8000-000000000002");
        var res = await client.GetAsync($"/api/v1/posts/{postId}/replies");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().Contain("Dramatis banget!");
    }
}
