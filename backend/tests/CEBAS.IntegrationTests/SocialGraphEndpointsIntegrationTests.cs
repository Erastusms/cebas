using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.SocialGraph;
using CEBAS.Application.Contracts.Users;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class SocialGraphEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SocialGraphEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
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
    public async Task Follow_WithoutAuth_ShouldReturn401Unauthorized()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsync($"/api/v1/users/{Guid.NewGuid()}/follow", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Block_WithoutAuth_ShouldReturn401Unauthorized()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsync($"/api/v1/users/{Guid.NewGuid()}/block", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Follow_Self_ShouldReturn400BadRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"self_{suffix}", $"self_{suffix}@test.com");

        var response = await client.PostAsync($"/api/v1/users/{user.Id}/follow", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(_jsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeNull();
        problem.Errors!.Values.SelectMany(v => v).Should().ContainMatch("*cannot follow themselves*");
    }

    [Fact]
    public async Task Block_Self_ShouldReturn400BadRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"bself_{suffix}", $"bself_{suffix}@test.com");

        var response = await client.PostAsync($"/api/v1/users/{user.Id}/block", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(_jsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeNull();
        problem.Errors!.Values.SelectMany(v => v).Should().ContainMatch("*cannot block themselves*");
    }

    [Fact]
    public async Task Follow_Unfollow_AndListEndpoints_ShouldWorkEndToEnd()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"user_a_{suffix}", $"user_a_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"user_b_{suffix}", $"user_b_{suffix}@test.com");

        // 1. User A follows User B
        var followRes = await clientA.PostAsync($"/api/v1/users/{userB.Id}/follow", null);
        followRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var followBody = await followRes.Content.ReadFromJsonAsync<ApiResponse<FollowResponse>>(_jsonOptions);
        followBody!.Data!.IsFollowing.Should().BeTrue();
        followBody.Data.TargetUserId.Should().Be(userB.Id);

        // 2. User B's followers list should contain User A
        var followersRes = await clientB.GetAsync($"/api/v1/users/{userB.Id}/followers?limit=10");
        followersRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var followersBody = await followersRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<SocialUserDto>>>(_jsonOptions);
        followersBody!.Data!.Items.Should().Contain(u => u.Id == userA.Id);

        // 3. User A's following list should contain User B
        var followingRes = await clientA.GetAsync($"/api/v1/users/{userA.Id}/following?limit=10");
        followingRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var followingBody = await followingRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<SocialUserDto>>>(_jsonOptions);
        followingBody!.Data!.Items.Should().Contain(u => u.Id == userB.Id);

        // 4. User B's public profile stats should reflect 1 follower
        var profileRes = await clientA.GetAsync($"/api/v1/users/{userB.Username}");
        profileRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var profileBody = await profileRes.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>(_jsonOptions);
        profileBody!.Data!.Stats.FollowerCount.Should().Be(1);
        profileBody.Data.Relationship!.IsFollowing.Should().BeTrue();

        // 5. User A unfollows User B
        var unfollowRes = await clientA.DeleteAsync($"/api/v1/users/{userB.Id}/follow");
        unfollowRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var unfollowBody = await unfollowRes.Content.ReadFromJsonAsync<ApiResponse<FollowResponse>>(_jsonOptions);
        unfollowBody!.Data!.IsFollowing.Should().BeFalse();

        // 6. User B's followers list should now be empty
        var followersAfterRes = await clientB.GetAsync($"/api/v1/users/{userB.Id}/followers");
        var followersAfterBody = await followersAfterRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<SocialUserDto>>>(_jsonOptions);
        followersAfterBody!.Data!.Items.Should().NotContain(u => u.Id == userA.Id);
    }

    [Fact]
    public async Task Block_ShouldAtomicallyRemoveMutualFollows_AndIsolateUsers()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"blk_a_{suffix}", $"blk_a_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"blk_b_{suffix}", $"blk_b_{suffix}@test.com");

        // Mutual follow: A -> B and B -> A
        await clientA.PostAsync($"/api/v1/users/{userB.Id}/follow", null);
        await clientB.PostAsync($"/api/v1/users/{userA.Id}/follow", null);

        // User A blocks User B
        var blockRes = await clientA.PostAsync($"/api/v1/users/{userB.Id}/block", null);
        blockRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var blockBody = await blockRes.Content.ReadFromJsonAsync<ApiResponse<BlockResponse>>(_jsonOptions);
        blockBody!.Data!.IsBlocked.Should().BeTrue();
        blockBody.Data.IsFollowing.Should().BeFalse();

        // Check mutual follow removal
        var aFollowing = await clientA.GetAsync($"/api/v1/users/{userA.Id}/following");
        var aFollowingBody = await aFollowing.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<SocialUserDto>>>(_jsonOptions);
        aFollowingBody!.Data!.Items.Should().NotContain(u => u.Id == userB.Id);

        var bFollowing = await clientB.GetAsync($"/api/v1/users/{userB.Id}/following");
        var bFollowingBody = await bFollowing.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<SocialUserDto>>>(_jsonOptions);
        bFollowingBody!.Data!.Items.Should().NotContain(u => u.Id == userA.Id);

        // Block isolation: User B cannot follow User A while blocked
        var bFollowA = await clientB.PostAsync($"/api/v1/users/{userA.Id}/follow", null);
        bFollowA.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Block isolation: User B visiting User A's profile gets 404
        var bViewA = await clientB.GetAsync($"/api/v1/users/{userA.Username}");
        bViewA.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // User A unblocks User B
        var unblockRes = await clientA.DeleteAsync($"/api/v1/users/{userB.Id}/block");
        unblockRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var unblockBody = await unblockRes.Content.ReadFromJsonAsync<ApiResponse<BlockResponse>>(_jsonOptions);
        unblockBody!.Data!.IsBlocked.Should().BeFalse();

        // Invariant: Unblocking does not restore previous follow relationships
        var aFollowingAfter = await clientA.GetAsync($"/api/v1/users/{userA.Id}/following");
        var aFollowingAfterBody = await aFollowingAfter.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<SocialUserDto>>>(_jsonOptions);
        aFollowingAfterBody!.Data!.Items.Should().NotContain(u => u.Id == userB.Id);
    }
}
