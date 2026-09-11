using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Search;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class SearchEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SearchEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchPosts_WithEmptyQuery_ShouldReturn200Ok_WithEmptyResult()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/search/posts?q=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CursorPagedResult<SearchPostItemDto>>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Items.Should().BeEmpty();
        apiResponse.Data.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task SearchUsers_WithEmptyQuery_ShouldReturn200Ok_WithEmptyResult()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/search/users?q=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<SearchUserItemDto>>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchSummary_WithEmptyQuery_ShouldReturn200Ok_WithEmptyResults()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/search?q=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<SearchSummaryResponse>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Posts.Should().BeEmpty();
        apiResponse.Data.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchUsers_WithQuery_WhenElasticsearchOffline_ShouldReturn200Ok_AndDegradeGracefully()
    {
        // Act - Exactly matching the user's scenario
        var response = await _client.GetAsync("/api/v1/search/users?q=john&limit=5");

        // Assert - Should not return 500 Internal Server Error
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<SearchUserItemDto>>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SearchSummary_WithQuery_WhenElasticsearchOffline_ShouldReturn200Ok_WithoutConcurrencyError()
    {
        // Act - Exactly matching the user's scenario
        var response = await _client.GetAsync("/api/v1/search?q=john&postLimit=5&userLimit=5");

        // Assert - Should not throw DbContext concurrency error and should return 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<SearchSummaryResponse>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchPosts_WithQuery_WhenElasticsearchOffline_ShouldReturn200Ok_AndDegradeGracefully()
    {
        // Act - Exactly matching query search for posts
        var response = await _client.GetAsync("/api/v1/search/posts?q=john&limit=5");

        // Assert - Should not return 500 Internal Server Error
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CursorPagedResult<SearchPostItemDto>>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetReadiness_ShouldContainElasticsearchCheck()
    {
        // Act
        var response = await _client.GetAsync("/readyz");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        root.TryGetProperty("checks", out var checks).Should().BeTrue();
        checks.TryGetProperty("elasticsearch", out var esProp).Should().BeTrue();
        esProp.GetString().Should().NotBeNullOrEmpty();
    }
}
