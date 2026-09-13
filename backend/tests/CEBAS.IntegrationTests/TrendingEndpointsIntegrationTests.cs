using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Trending;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class TrendingEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TrendingEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTrends_Returns200Ok_WithTrendingEnvelope()
    {
        var response = await _client.GetAsync("/api/v1/trends");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<TrendingTopicsResult>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTrends_WithCustomLimit_Returns200Ok()
    {
        var response = await _client.GetAsync("/api/v1/trends?limit=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<TrendingTopicsResult>>(content, JsonOptions);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Items.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task GetTrends_WithInvalidLimit_Returns400BadRequest()
    {
        var response = await _client.GetAsync("/api/v1/trends?limit=100");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
