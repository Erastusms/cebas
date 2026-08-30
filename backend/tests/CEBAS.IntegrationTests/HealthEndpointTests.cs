using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;

namespace CEBAS.IntegrationTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ShouldReturn200Ok_WithHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        json.RootElement.GetProperty("service").GetString().Should().Be("CEBAS API");
    }

    [Fact]
    public async Task GetPing_ShouldReturn200Ok_WithPongMessage()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/ping");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.GetProperty("message").GetString().Should().Be("pong");
    }

    [Fact]
    public async Task ErrorTest_Validation_ShouldReturn400_WithRfc7807ProblemDetails()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/error-test?type=validation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetailsResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Validation Error");
        problem.Errors.Should().ContainKey("username");
        problem.TraceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ErrorTest_NotFound_ShouldReturn404_WithRfc7807ProblemDetails()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/error-test?type=notfound");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetailsResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Resource Not Found");
        problem.TraceId.Should().NotBeNullOrEmpty();
    }
}
