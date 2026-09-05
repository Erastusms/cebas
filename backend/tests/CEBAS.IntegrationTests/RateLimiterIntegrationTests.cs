using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Contracts.Auth;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class RateLimiterIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimiterIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticationPolicy_ShouldTrigger429AndRetryAfterHeader_WhenExceedingPermitLimit()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"172.16.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}");

        // The Authentication policy has a limit of 10 requests per minute
        HttpResponseMessage? throttledResponse = null;

        for (int i = 0; i < 15; i++)
        {
            var loginRequest = new LoginRequest($"test_burst_{i}", "Password123!");
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throttledResponse = response;
                break;
            }
        }

        throttledResponse.Should().NotBeNull("Sending 15 rapid authentication requests should trigger rate limiting");
        throttledResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Verify Retry-After header
        throttledResponse.Headers.Contains("Retry-After").Should().BeTrue();
        var retryAfterValue = throttledResponse.Headers.GetValues("Retry-After").FirstOrDefault();
        retryAfterValue.Should().NotBeNull();
        int.TryParse(retryAfterValue, out int retryAfterSeconds).Should().BeTrue();
        retryAfterSeconds.Should().BeGreaterThan(0);

        // Verify RFC 7807 problem details response body
        var content = await throttledResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Too Many Requests");
        content.Should().Contain("429");
    }
}
