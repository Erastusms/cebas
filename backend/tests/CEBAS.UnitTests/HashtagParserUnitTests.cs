using FluentAssertions;
using Xunit;
using CEBAS.Api.Features.Hashtags.ExtractHashtags;

namespace CEBAS.UnitTests;

public class HashtagParserUnitTests
{
    private readonly HashtagParser _parser = new();

    [Theory]
    [InlineData("#hello", "hello", "hello")]
    [InlineData("#hello_world", "hello_world", "hello_world")]
    [InlineData("#hello123", "hello123", "hello123")]
    [InlineData("#CEBAS", "cebas", "CEBAS")]
    [InlineData("#Topic123", "topic123", "Topic123")]
    public void ExtractHashtags_SingleHashtag_ExtractsNormalizedAndDisplayName(string content, string expectedNormalized, string expectedDisplay)
    {
        var result = _parser.ExtractHashtags(content);

        result.Should().HaveCount(1);
        result[0].NormalizedName.Should().Be(expectedNormalized);
        result[0].DisplayName.Should().Be(expectedDisplay);
    }

    [Fact]
    public void ExtractHashtags_MultipleHashtags_ExtractsAllDistinctTags()
    {
        var content = "Welcome to #CEBAS! Check out #social_media and #indonesia.";
        var result = _parser.ExtractHashtags(content);

        result.Should().HaveCount(3);
        result.Select(r => r.NormalizedName).Should().Equal("cebas", "social_media", "indonesia");
    }

    [Fact]
    public void ExtractHashtags_DuplicateHashtags_DeduplicatesAndPreservesFirstDisplay()
    {
        var content = "Loving #CEBAS so much! #cebas is great, absolutely #Cebas.";
        var result = _parser.ExtractHashtags(content);

        result.Should().HaveCount(1);
        result[0].NormalizedName.Should().Be("cebas");
        result[0].DisplayName.Should().Be("CEBAS");
    }

    [Fact]
    public void ExtractHashtags_StandaloneHash_DoesNotExtract()
    {
        var content = "This # is not a hashtag and neither is # followed by spaces.";
        var result = _parser.ExtractHashtags(content);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("hello, #world!", "world")]
    [InlineData("check this: (#CEBAS)", "cebas")]
    [InlineData("[#technology]", "technology")]
    [InlineData("{#startup}", "startup")]
    [InlineData("#tag.", "tag")]
    [InlineData("#tag...", "tag")]
    [InlineData("#tag?", "tag")]
    [InlineData("#tag;", "tag")]
    public void ExtractHashtags_AdjacentPunctuation_ExtractsCleanTag(string content, string expectedNormalized)
    {
        var result = _parser.ExtractHashtags(content);

        result.Should().HaveCount(1);
        result[0].NormalizedName.Should().Be(expectedNormalized);
    }

    [Fact]
    public void ExtractHashtags_PositionsInText_BeginningMiddleEnd()
    {
        var beginning = "#FirstTag in the sentence";
        var middle = "A sentence with #MiddleTag inside";
        var end = "A sentence ending with #LastTag";

        _parser.ExtractHashtags(beginning).Should().ContainSingle(t => t.NormalizedName == "firsttag");
        _parser.ExtractHashtags(middle).Should().ContainSingle(t => t.NormalizedName == "middletag");
        _parser.ExtractHashtags(end).Should().ContainSingle(t => t.NormalizedName == "lasttag");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Just plain text with no tags.")]
    public void ExtractHashtags_EmptyOrNoTags_ReturnsEmptyList(string? content)
    {
        var result = _parser.ExtractHashtags(content);
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("contact user#domain.com for info")]
    [InlineData("abc#123 should not match")]
    [InlineData("##double_hash should not match")]
    [InlineData("email@#tag should not match")]
    public void ExtractHashtags_InvalidContext_DoesNotExtractInvalidHashtag(string content)
    {
        var result = _parser.ExtractHashtags(content);
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("#CEBAS", "cebas")]
    [InlineData("cebas", "cebas")]
    [InlineData("#Hello_World", "hello_world")]
    [InlineData("  #Topic123  ", "topic123")]
    public void Normalize_ReturnsCleanLowercase(string tag, string expected)
    {
        _parser.Normalize(tag).Should().Be(expected);
    }
}
