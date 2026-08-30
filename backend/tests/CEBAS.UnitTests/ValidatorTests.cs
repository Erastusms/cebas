using FluentAssertions;
using Xunit;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Users;

namespace CEBAS.UnitTests;

public class ValidatorTests
{
    [Fact]
    public void RegisterRequestValidator_WithValidInput_ShouldPass()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("johndoe", "john@example.com", "SecurePassword123!", "John Doe");

        var result = validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("ab", "Username must be at least 3 characters.")]
    [InlineData("invalid user", "Username may only contain letters, numbers, and underscores.")]
    [InlineData("", "Username is required.")]
    public void RegisterRequestValidator_WithInvalidUsername_ShouldFail(string username, string expectedError)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(username, "john@example.com", "SecurePassword123!", "John Doe");

        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError));
    }

    [Fact]
    public void RegisterRequestValidator_WithShortPassword_ShouldFail()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("johndoe", "john@example.com", "short", "John Doe");

        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void UpdateProfileRequestValidator_WithValidData_ShouldPass()
    {
        var validator = new UpdateProfileRequestValidator();
        var request = new UpdateProfileRequest("New Display Name", "Short bio description.");

        var result = validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateProfileRequestValidator_WithBioExceeding160Chars_ShouldFail()
    {
        var validator = new UpdateProfileRequestValidator();
        var longBio = new string('x', 161);
        var request = new UpdateProfileRequest("Valid Name", longBio);

        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Bio");
    }
}
