using System.Text.Json;
using FluentAssertions;
using Xunit;
using CEBAS.Application.Common;

namespace CEBAS.UnitTests;

public class ProblemDetailsSerializationTests
{
    [Fact]
    public void ProblemDetailsResponse_ShouldSerializeToRfc7807Json()
    {
        // Arrange
        var problem = new ProblemDetailsResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Validation Error",
            Status = 400,
            Detail = "One or more validation failures occurred.",
            Instance = "/api/v1/posts",
            TraceId = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            Errors = new Dictionary<string, string[]>
            {
                { "content", ["Content cannot exceed 1000 characters."] }
            }
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // Act
        var json = JsonSerializer.Serialize(problem, options);
        var deserialized = JsonSerializer.Deserialize<ProblemDetailsResponse>(json, options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Title.Should().Be("Validation Error");
        deserialized.Status.Should().Be(400);
        deserialized.Errors.Should().ContainKey("content");
        deserialized.Errors!["content"].Should().Contain("Content cannot exceed 1000 characters.");
    }
}
