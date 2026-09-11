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
public class ThemePreferenceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ThemePreferenceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginRequest = new LoginRequest("johndoe", "Password123!");
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return client;
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsThemePreference()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        body.Data!.ThemePreference.Should().NotBeNullOrWhiteSpace();
        new[] { "LIGHT", "DARK", "SYSTEM" }.Should().Contain(body.Data.ThemePreference);
    }

    [Fact]
    public async Task UpdateThemePreference_WithValidValues_SucceedsAndPersists()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Update to DARK
        var darkPatch = new UpdateProfileRequest(ThemePreference: "DARK");
        var darkResponse = await client.PatchAsJsonAsync("/api/v1/users/me", darkPatch);
        darkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var darkBody = await darkResponse.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);
        darkBody!.Data!.ThemePreference.Should().Be("DARK");

        // Verify with GET
        var getDark = await client.GetAsync("/api/v1/users/me");
        var getDarkBody = await getDark.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);
        getDarkBody!.Data!.ThemePreference.Should().Be("DARK");

        // 2. Update to LIGHT
        var lightPatch = new UpdateProfileRequest(ThemePreference: "LIGHT");
        var lightResponse = await client.PatchAsJsonAsync("/api/v1/users/me", lightPatch);
        lightResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var lightBody = await lightResponse.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);
        lightBody!.Data!.ThemePreference.Should().Be("LIGHT");

        // 3. Update to SYSTEM
        var systemPatch = new UpdateProfileRequest(ThemePreference: "SYSTEM");
        var systemResponse = await client.PatchAsJsonAsync("/api/v1/users/me", systemPatch);
        systemResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var systemBody = await systemResponse.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);
        systemBody!.Data!.ThemePreference.Should().Be("SYSTEM");
    }

    [Theory]
    [InlineData("light-mode")]
    [InlineData("darkmode")]
    [InlineData("foo")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public async Task UpdateThemePreference_WithInvalidValues_Returns400BadRequest(string invalidTheme)
    {
        var client = await CreateAuthenticatedClientAsync();

        var invalidPatch = new UpdateProfileRequest(ThemePreference: invalidTheme);
        var response = await client.PatchAsJsonAsync("/api/v1/users/me", invalidPatch);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(_jsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeNull();
        problem.Errors!.Keys.Should().Contain("ThemePreference");
    }

    [Fact]
    public async Task UpdateThemePreference_Unauthenticated_Returns401Unauthorized()
    {
        var unauthClient = _factory.CreateClient();

        var patch = new UpdateProfileRequest(ThemePreference: "DARK");
        var response = await unauthClient.PatchAsJsonAsync("/api/v1/users/me", patch);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
