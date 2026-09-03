using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Domain.Common;

namespace CEBAS.UnitTests;

public class CursorEngineTests
{
    [Fact]
    public void Cursor_EncodeAndDecode_ShouldRoundTripWithPrecision()
    {
        var originalId = Uuid7.New();
        var originalTime = DateTimeOffset.UtcNow;
        var cursor = new Cursor(originalTime, originalId);

        var encoded = cursor.Encode();
        var decoded = Cursor.Decode(encoded);

        decoded.Should().NotBeNull();
        decoded!.Id.Should().Be(originalId);
        decoded.CreatedAt.Ticks.Should().Be(originalTime.Ticks);
    }

    [Fact]
    public void Cursor_Decode_LegacyMillisecondPayload_ShouldDecodeCorrectly()
    {
        var id = Uuid7.New();
        var now = DateTimeOffset.UtcNow;
        var legacyJson = JsonSerializer.Serialize(new { c = now.ToUnixTimeMilliseconds(), i = id });
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(legacyJson));

        var decoded = Cursor.Decode(base64);

        decoded.Should().NotBeNull();
        decoded!.Id.Should().Be(id);
        decoded.CreatedAt.ToUnixTimeMilliseconds().Should().Be(now.ToUnixTimeMilliseconds());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cursor_TryDecode_NullOrWhitespace_ShouldReturnTrueWithNullCursor(string? input)
    {
        var success = Cursor.TryDecode(input, out var cursor, out var error);

        success.Should().BeTrue();
        cursor.Should().BeNull();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("not-valid-base64!@#$")]
    [InlineData("===")]
    [InlineData("???")]
    public void Cursor_TryDecode_MalformedBase64_ShouldReturnFalseWithErrorMessage(string malformed)
    {
        var success = Cursor.TryDecode(malformed, out var cursor, out var error);

        success.Should().BeFalse();
        cursor.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("Base64");
    }

    [Fact]
    public void Cursor_TryDecode_CorruptedJson_ShouldReturnFalseWithErrorMessage()
    {
        var invalidJson = "this is not json";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(invalidJson));

        var success = Cursor.TryDecode(base64, out var cursor, out var error);

        success.Should().BeFalse();
        cursor.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Cursor_TryDecode_EmptyGuid_ShouldReturnFalseWithErrorMessage()
    {
        var payload = JsonSerializer.Serialize(new { c = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), i = Guid.Empty });
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        var success = Cursor.TryDecode(base64, out var cursor, out var error);

        success.Should().BeFalse();
        cursor.Should().BeNull();
        error.Should().Contain("identifier");
    }

    [Fact]
    public void Cursor_TryDecode_FutureTimestamp_ShouldReturnFalseWithErrorMessage()
    {
        var futureTime = DateTimeOffset.UtcNow.AddDays(10);
        var payload = JsonSerializer.Serialize(new { u = futureTime.Ticks, i = Uuid7.New() });
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        var success = Cursor.TryDecode(base64, out var cursor, out var error);

        success.Should().BeFalse();
        cursor.Should().BeNull();
        error.Should().Contain("future");
    }

    [Fact]
    public void Cursor_TryDecode_AncientTimestamp_ShouldReturnFalseWithErrorMessage()
    {
        var ancientTime = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var payload = JsonSerializer.Serialize(new { u = ancientTime.Ticks, i = Uuid7.New() });
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        var success = Cursor.TryDecode(base64, out var cursor, out var error);

        success.Should().BeFalse();
        cursor.Should().BeNull();
        error.Should().Contain("bounds");
    }

    [Fact]
    public void CursorPagedResult_Create_ShouldComputePaginationAndNextCursorCorrectly()
    {
        var id1 = Uuid7.New();
        var id2 = Uuid7.New();
        var id3 = Uuid7.New();
        var time = DateTimeOffset.UtcNow;

        var items = new List<(DateTimeOffset CreatedAt, Guid Id)>
        {
            (time.AddSeconds(-1), id1),
            (time.AddSeconds(-2), id2),
            (time.AddSeconds(-3), id3)
        };

        // pageSize = 2 with 3 items -> hasNextPage = true
        var result = CursorPagedResult<(DateTimeOffset CreatedAt, Guid Id)>.Create(
            items,
            pageSize: 2,
            item => new Cursor(item.CreatedAt, item.Id)
        );

        result.Items.Should().HaveCount(2);
        result.HasNextPage.Should().BeTrue();
        result.NextCursor.Should().NotBeNullOrEmpty();

        var decodedNext = Cursor.Decode(result.NextCursor);
        decodedNext.Should().NotBeNull();
        decodedNext!.Id.Should().Be(id2);
        decodedNext.CreatedAt.Ticks.Should().Be(time.AddSeconds(-2).Ticks);

        // When items <= pageSize -> hasNextPage = false
        var singlePageResult = CursorPagedResult<(DateTimeOffset CreatedAt, Guid Id)>.Create(
            items.Take(2).ToList(),
            pageSize: 2,
            item => new Cursor(item.CreatedAt, item.Id)
        );

        singlePageResult.Items.Should().HaveCount(2);
        singlePageResult.HasNextPage.Should().BeFalse();
        singlePageResult.NextCursor.Should().BeNull();
    }

    [Fact]
    public void Cursor_DeterministicTieBreaker_ShouldOrderCorrectlyWhenTimestampsIdentical()
    {
        var identicalTime = DateTimeOffset.UtcNow;
        var guidA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var guidB = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var list = new List<(DateTimeOffset CreatedAt, Guid Id)>
        {
            (identicalTime, guidA),
            (identicalTime, guidB)
        };

        // ORDER BY created_at DESC, id DESC
        var ordered = list
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToList();

        ordered[0].Id.Should().Be(guidB);
        ordered[1].Id.Should().Be(guidA);

        // Cursor comparison: (CreatedAt, Id) < (cursor.CreatedAt, cursor.Id)
        var cursor = new Cursor(identicalTime, guidB);
        bool shouldIncludeA = (identicalTime < cursor.CreatedAt) ||
                              (identicalTime == cursor.CreatedAt && guidA.CompareTo(cursor.Id) < 0);

        shouldIncludeA.Should().BeTrue();
    }
}
