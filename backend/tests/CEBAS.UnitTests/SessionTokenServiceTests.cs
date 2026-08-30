using FluentAssertions;
using Xunit;
using CEBAS.Infrastructure.Services;

namespace CEBAS.UnitTests;

public class SessionTokenServiceTests
{
    private readonly SessionTokenService _service = new();

    [Fact]
    public void GenerateRawToken_ShouldGenerateUnique64HexCharToken()
    {
        string token1 = _service.GenerateRawToken();
        string token2 = _service.GenerateRawToken();

        token1.Should().NotBeNullOrWhiteSpace();
        token1.Length.Should().Be(64); // 32 bytes = 64 hex characters
        token2.Length.Should().Be(64);
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void ComputeTokenHash_ShouldProduceDeterministicSha256Hex()
    {
        string token = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        string hash1 = _service.ComputeTokenHash(token);
        string hash2 = _service.ComputeTokenHash(token);

        hash1.Should().NotBeNullOrWhiteSpace();
        hash1.Length.Should().Be(64);
        hash1.Should().Be(hash2);
    }
}
