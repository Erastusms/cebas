using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Users;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class AuthEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuthEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
    }

    [Fact]
    public async Task Register_WithInvalidData_ShouldReturn400BadRequest_WithProblemDetails()
    {
        var invalidRequest = new RegisterRequest("ab", "invalid-email", "short", "");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", invalidRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(_jsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Keys.Should().Contain("Username");
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ShouldReturn400BadRequest_WithProblemDetails()
    {
        var invalidRequest = new LoginRequest("", "");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", invalidRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthenticationCookie_ShouldReturn401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_WithoutAuthenticationCookie_ShouldReturn401Unauthorized()
    {
        var updateRequest = new UpdateProfileRequest("New Name", "New Bio");
        var response = await _client.PatchAsJsonAsync("/api/v1/users/me", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserSessions_WithoutAuthenticationCookie_ShouldReturn401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/users/me/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WhenUnauthenticated_ShouldBeIdempotentAndReturn200()
    {
        var response = await _client.PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WithSeededUser_ShouldSucceed_AndAllowAccessToProtectedEndpoints()
    {
        var loginRequest = new LoginRequest("johndoe", "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        loginResponse.Headers.Should().ContainKey("Set-Cookie");

        // Verify GET /api/v1/users/me works with the session cookie
        var meResponse = await _client.GetAsync("/api/v1/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meBody = await meResponse.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);
        meBody.Should().NotBeNull();
        meBody!.Data!.Username.Should().Be("johndoe");

        // Verify GET /api/v1/users/me/sessions returns active sessions
        var sessionsResponse = await _client.GetAsync("/api/v1/users/me/sessions");
        sessionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionsBody = await sessionsResponse.Content.ReadFromJsonAsync<ApiResponse<List<SessionItemResponse>>>(_jsonOptions);
        sessionsBody.Should().NotBeNull();
        sessionsBody!.Data!.Should().NotBeEmpty();
        sessionsBody.Data!.Should().Contain(s => s.IsCurrent);

        // Verify public profile lookup works case-insensitively
        var publicProfileResponse = await _client.GetAsync("/api/v1/users/JohnDoe");
        publicProfileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publicBody = await publicProfileResponse.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>(_jsonOptions);
        publicBody.Should().NotBeNull();
        publicBody!.Data!.Username.Should().Be("johndoe");
        publicBody.Data!.DisplayName.Should().Be("John Doe");
    }
}
