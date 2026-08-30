using FluentAssertions;
using Xunit;
using CEBAS.Infrastructure.Services;

namespace CEBAS.UnitTests;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ShouldProduceSecureHash_AndNotMatchPlaintext()
    {
        string password = "MySecurePassword123!";
        string hash = _hasher.Hash(password);

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBe(password);
        hash.Should().StartWith(""); // BCrypt prefix
    }

    [Fact]
    public void Verify_WithCorrectPassword_ShouldReturnTrue()
    {
        string password = "CorrectPassword123#";
        string hash = _hasher.Hash(password);

        bool result = _hasher.Verify(password, hash);
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_ShouldReturnFalse()
    {
        string password = "CorrectPassword123#";
        string wrongPassword = "WrongPassword456$";
        string hash = _hasher.Hash(password);

        bool result = _hasher.Verify(wrongPassword, hash);
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithCorruptedHash_ShouldReturnFalse()
    {
        bool result = _hasher.Verify("AnyPassword123", "invalid-hash-string");
        result.Should().BeFalse();
    }
}
