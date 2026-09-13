using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Application.Contracts.Users;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class TagTimelineIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TagTimelineIntegrationTests(WebApplicationFactory<Program> factory)
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
    public async Task TagTimeline_ReturnsPostsTaggedWithHashtag()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"tag_u1_{suffix}", $"tag_u1_{suffix}@test.com");

        var tag = $"tag{suffix}";
        var resPost1 = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Post one with #{tag} included", null));
        resPost1.StatusCode.Should().Be(HttpStatusCode.Created);
        var post1 = (await resPost1.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        var resPost2 = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Post two without tag", null));
        resPost2.StatusCode.Should().Be(HttpStatusCode.Created);

        var resPost3 = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Post three also with #{tag} included", null));
        resPost3.StatusCode.Should().Be(HttpStatusCode.Created);
        var post3 = (await resPost3.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // Query tag timeline (unauthenticated client or authenticated client)
        var response = await client.GetAsync($"/api/v1/timelines/tags/{tag}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();

        var items = body.Data!.Items;
        items.Should().HaveCount(2);
        items.Select(p => p.Id).Should().Contain(post1.Id);
        items.Select(p => p.Id).Should().Contain(post3.Id);
    }

    [Fact]
    public async Task TagTimeline_CaseInsensitiveTagMatching_ReturnsIdenticalResults()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"tag_u2_{suffix}", $"tag_u2_{suffix}@test.com");

        var tag = $"TrendCase_{suffix}";
        var resPost = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Testing #{tag} casing", null));
        resPost.StatusCode.Should().Be(HttpStatusCode.Created);
        var post = (await resPost.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // Query lowercase
        var resLower = await client.GetAsync($"/api/v1/timelines/tags/{tag.ToLowerInvariant()}");
        resLower.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyLower = await resLower.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);

        // Query uppercase
        var resUpper = await client.GetAsync($"/api/v1/timelines/tags/{tag.ToUpperInvariant()}");
        resUpper.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyUpper = await resUpper.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);

        bodyLower!.Data!.Items.Should().ContainSingle(p => p.Id == post.Id);
        bodyUpper!.Data!.Items.Should().ContainSingle(p => p.Id == post.Id);
    }

    [Fact]
    public async Task TagTimeline_DeletedPost_IsExcludedFromFeed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"tag_u3_{suffix}", $"tag_u3_{suffix}@test.com");

        var tag = $"del_tag_{suffix}";
        var resPost = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Post to delete #{tag}", null));
        resPost.StatusCode.Should().Be(HttpStatusCode.Created);
        var post = (await resPost.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // Delete post
        var delRes = await client.DeleteAsync($"/api/v1/posts/{post.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify excluded from tag feed
        var response = await client.GetAsync($"/api/v1/timelines/tags/{tag}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        body!.Data!.Items.Should().NotContain(p => p.Id == post.Id);
    }

    [Fact]
    public async Task TagTimeline_BlockedUser_IsExcludedFromFeed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"tag_blk_a_{suffix}", $"tag_blk_a_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"tag_blk_b_{suffix}", $"tag_blk_b_{suffix}@test.com");

        var tag = $"block_tag_{suffix}";

        // User B posts with tag
        var resPostB = await clientB.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest($"Post from B #{tag}", null));
        resPostB.StatusCode.Should().Be(HttpStatusCode.Created);
        var postB = (await resPostB.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions))!.Data!;

        // User A blocks User B
        var blockRes = await clientA.PostAsync($"/api/v1/users/{userB.Id}/block", null);
        blockRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // User A views tag feed -> User B's post must be excluded
        var feedA = await clientA.GetAsync($"/api/v1/timelines/tags/{tag}");
        var bodyA = await feedA.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        bodyA!.Data!.Items.Should().NotContain(p => p.Id == postB.Id);
    }
}
