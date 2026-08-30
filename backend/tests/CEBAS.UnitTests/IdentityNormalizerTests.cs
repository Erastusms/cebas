using FluentAssertions;
using Xunit;
using CEBAS.Domain.Common;

namespace CEBAS.UnitTests;

public class IdentityNormalizerTests
{
    [Theory]
    [InlineData("JohnDoe", "johndoe")]
    [InlineData("  user_123  ", "user_123")]
    [InlineData("ADMIN_USER", "admin_user")]
    public void NormalizeUsername_ShouldTrimAndConvertToLowercase(string input, string expected)
    {
        var result = IdentityNormalizers.NormalizeUsername(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("user123", true)]
    [InlineData("john_doe", true)]
    [InlineData("abc", true)]
    [InlineData("a_very_long_username_that_is30", true)]
    [InlineData("ab", false)] // too short
    [InlineData("a_very_long_username_that_exceeds_thirty_chars", false)] // too long
    [InlineData("invalid user", false)] // space
    [InlineData("user@name", false)] // invalid symbol
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidUsername_ShouldValidateAccordingToRules(string? input, bool expected)
    {
        var result = IdentityNormalizers.IsValidUsername(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Test@Example.COM", "test@example.com")]
    [InlineData("  user.name+tag@domain.co.id  ", "user.name+tag@domain.co.id")]
    public void NormalizeEmail_ShouldTrimAndConvertToLowercase(string input, string expected)
    {
        var result = IdentityNormalizers.NormalizeEmail(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user+tag@domain.co.uk", true)]
    [InlineData("invalid-email", false)]
    [InlineData("@missinguser.com", false)]
    [InlineData("missingdomain@", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidEmail_ShouldValidateAccordingToRules(string? input, bool expected)
    {
        var result = IdentityNormalizers.IsValidEmail(input);
        result.Should().Be(expected);
    }
}
