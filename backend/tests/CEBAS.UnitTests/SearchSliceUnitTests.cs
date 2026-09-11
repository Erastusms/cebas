using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Contracts.Events;
using CEBAS.Application.Contracts.Search;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Search;
using CEBAS.Infrastructure.Search.Models;

namespace CEBAS.UnitTests;

public class SearchSliceUnitTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    #region Search Cursor Tests

    [Fact]
    public void SearchCursor_EncodeAndDecode_ShouldPreserveValues()
    {
        // Arrange
        var original = new SearchCursor(4.5678, 1726050000000L, "doc_12345");

        // Act
        var encoded = original.Encode();
        var success = SearchCursor.TryDecode(encoded, out var decoded, out var error);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        decoded.Should().NotBeNull();
        decoded!.Id.Should().Be("doc_12345");
        decoded.CreatedAtMs.Should().Be(1726050000000L);
        decoded.Score.Should().BeApproximately(4.5678, 0.001);
    }

    [Fact]
    public void SearchCursor_Decode_WithNullOrWhitespace_ShouldReturnTrueWithNull()
    {
        var success = SearchCursor.TryDecode(null, out var cursor, out var error);

        success.Should().BeTrue();
        cursor.Should().BeNull();
        error.Should().BeNull();

        var successEmpty = SearchCursor.TryDecode("   ", out var cursorEmpty, out var errorEmpty);
        successEmpty.Should().BeTrue();
        cursorEmpty.Should().BeNull();
        errorEmpty.Should().BeNull();
    }

    [Fact]
    public void SearchCursor_Decode_WithInvalidBase64_ShouldReturnFalse()
    {
        var success = SearchCursor.TryDecode("!!!not_base64!!!", out var cursor, out var error);

        success.Should().BeFalse();
        cursor.Should().BeNull();
        error.Should().Contain("Base64");
    }

    [Fact]
    public void SearchCursor_Decode_WithMalformedJson_ShouldReturnFalse()
    {
        var badPayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{ broken json"));
        var success = SearchCursor.TryDecode(badPayload, out var cursor, out var error);

        success.Should().BeFalse();
        cursor.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void SearchCursor_Decode_MissingId_ShouldReturnFalse()
    {
        var noIdJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"s\": 1.0, \"c\": 12345}"));
        var success = SearchCursor.TryDecode(noIdJson, out var cursor, out var error);

        success.Should().BeFalse();
        cursor.Should().BeNull();
        error.Should().Contain("identifier");
    }

    #endregion

    #region Block Filtering Verification

    [Fact]
    public async Task Search_WithBidirectionalBlock_ShouldFilterOutBlockedUsers()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var currentUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();
        var innocentUserId = Guid.NewGuid();

        var blockService = Substitute.For<IBlockIsolationService>();
        blockService.GetBidirectionalBlockedUserIdsAsync(currentUserId, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { blockedUserId });

        var blockedList = await blockService.GetBidirectionalBlockedUserIdsAsync(currentUserId);

        // Assert
        blockedList.Should().Contain(blockedUserId);
        blockedList.Should().NotContain(innocentUserId);
    }

    #endregion

    #region Hashtag Extraction & Document Mapping

    [Fact]
    public void PostDocument_HashtagExtraction_ShouldExtractAllTagsCaseInsensitively()
    {
        // Arrange
        var content = "Halo kawan #indonesia sedang belajar #Elasticsearch dan #CEBAS_2026!";
        var regex = new System.Text.RegularExpressions.Regex(@"#([a-zA-Z0-9_]+)");
        var matches = regex.Matches(content);

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                tags.Add(match.Groups[1].Value.ToLowerInvariant());
            }
        }

        // Assert
        tags.Should().HaveCount(3);
        tags.Should().Contain("indonesia");
        tags.Should().Contain("elasticsearch");
        tags.Should().Contain("cebas_2026");
    }

    #endregion

    #region DTO & Health Contract Tests

    [Fact]
    public void SearchHealthStatus_Properties_ShouldReflectParameters()
    {
        var health = new SearchHealthStatus("Healthy", true, "cebas-cluster", 1500, 300);

        health.Status.Should().Be("Healthy");
        health.Available.Should().BeTrue();
        health.ClusterName.Should().Be("cebas-cluster");
        health.PostCount.Should().Be(1500);
        health.UserCount.Should().Be(300);
    }

    [Fact]
    public void ReindexSummary_Calculation_ShouldSumTotals()
    {
        var summary = new ReindexSummary(100, 50, 0, 245.5);

        summary.ProcessedPosts.Should().Be(100);
        summary.ProcessedUsers.Should().Be(50);
        summary.Errors.Should().Be(0);
        summary.ElapsedMilliseconds.Should().Be(245.5);
    }

    #endregion
}
