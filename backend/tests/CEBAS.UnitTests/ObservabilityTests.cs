using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using CEBAS.Infrastructure.Observability;

namespace CEBAS.UnitTests;

public class ObservabilityTests
{
    [Theory]
    [InlineData("password", "SuperSecret123!")]
    [InlineData("token", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...")]
    [InlineData("accessToken", "ya29.a0AfH6SM...")]
    [InlineData("refreshToken", "refresh_token_xyz")]
    [InlineData("secret", "cebas_admin_key_999")]
    [InlineData("authorization", "Bearer my_secret_token")]
    [InlineData("cookie", "cebas_session=secret_session_token")]
    public void SensitiveDataMaskingEnricher_ShouldRedactSensitiveKeyNames(string propertyName, string sensitiveValue)
    {
        // Arrange
        var enricher = new SensitiveDataMaskingEnricher();
        var messageTemplate = new MessageTemplateParser().Parse("User logged in with credentials");
        var property = new LogEventProperty(propertyName, new ScalarValue(sensitiveValue));
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            messageTemplate,
            new[] { property });

        // Act
        enricher.Enrich(logEvent, null!);

        // Assert
        var enrichedProp = logEvent.Properties[propertyName];
        enrichedProp.Should().NotBeNull();
        enrichedProp.ToString().Should().Contain(SensitiveDataMaskingEnricher.RedactedPlaceholder);
        enrichedProp.ToString().Should().NotContain(sensitiveValue);
    }

    [Fact]
    public void SensitiveDataMaskingEnricher_ShouldMaskBearerTokensInStrings()
    {
        // Arrange
        const string rawText = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.signature";
        
        // Act
        var masked = SensitiveDataMaskingEnricher.MaskSensitiveText(rawText);

        // Assert
        masked.Should().Be($"Bearer {SensitiveDataMaskingEnricher.RedactedPlaceholder}");
        masked.Should().NotContain("payload");
    }

    [Fact]
    public void CebasActivitySource_ShouldBeProperlyNamedAndVersioned()
    {
        CebasActivitySource.SourceName.Should().Be("CEBAS");
        CebasActivitySource.Instance.Name.Should().Be("CEBAS");
        CebasActivitySource.Instance.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void CebasMetrics_ShouldHaveInitializedMetersAndCounters()
    {
        CebasMetrics.MeterName.Should().Be("CEBAS.Core");
        CebasMetrics.HttpRequestCount.Should().NotBeNull();
        CebasMetrics.HttpErrorCount.Should().NotBeNull();
        CebasMetrics.HttpRequestDuration.Should().NotBeNull();
        CebasMetrics.CommandExecutionCount.Should().NotBeNull();
        CebasMetrics.QueryExecutionCount.Should().NotBeNull();
        CebasMetrics.DatabaseQueryDuration.Should().NotBeNull();
        CebasMetrics.ActiveRealtimeConnections.Should().NotBeNull();
        CebasMetrics.NotificationsGenerated.Should().NotBeNull();
        CebasMetrics.ReportsCreated.Should().NotBeNull();
    }
}
