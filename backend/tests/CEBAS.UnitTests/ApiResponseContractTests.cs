using FluentAssertions;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Domain.Common;

namespace CEBAS.UnitTests;

public class ApiResponseContractTests
{
    [Fact]
    public void ApiResponse_Ok_ShouldConstructValidResponseEnvelope()
    {
        // Arrange
        var payload = new { username = "testuser", email = "test@example.com" };

        // Act
        var response = ApiResponse<object>.Ok(payload, "Operation successful");

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(payload);
        response.Message.Should().Be("Operation successful");
    }

    [Fact]
    public void Cursor_EncodeAndDecode_ShouldRoundTripCorrectly()
    {
        // Arrange
        var originalId = Uuid7.New();
        var originalTime = DateTimeOffset.UtcNow;
        var cursor = new Cursor(originalTime, originalId);

        // Act
        var encoded = cursor.Encode();
        var decoded = Cursor.Decode(encoded);

        // Assert
        decoded.Should().NotBeNull();
        decoded!.Id.Should().Be(originalId);
        decoded.CreatedAt.ToUnixTimeMilliseconds().Should().Be(originalTime.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void CursorPagedResult_Create_ShouldComputePaginationCorrectly()
    {
        // Arrange
        var items = new List<(DateTimeOffset CreatedAt, Guid Id)>
        {
            (DateTimeOffset.UtcNow.AddMinutes(-3), Uuid7.New()),
            (DateTimeOffset.UtcNow.AddMinutes(-2), Uuid7.New()),
            (DateTimeOffset.UtcNow.AddMinutes(-1), Uuid7.New())
        };

        // Act
        var paged = CursorPagedResult<(DateTimeOffset CreatedAt, Guid Id)>.Create(
            items,
            pageSize: 2,
            item => new Cursor(item.CreatedAt, item.Id)
        );

        // Assert
        paged.Items.Count.Should().Be(2);
        paged.HasNextPage.Should().BeTrue();
        paged.NextCursor.Should().NotBeNullOrEmpty();
    }
}
