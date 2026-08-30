using FluentAssertions;
using Xunit;
using CEBAS.Domain.Common;

namespace CEBAS.UnitTests;

public class Uuid7Tests
{
    [Fact]
    public void New_ShouldGenerateValidUuidVersion7()
    {
        // Act
        var uuid = Uuid7.New();

        // Assert
        uuid.Should().NotBeEmpty();
        Uuid7.IsVersion7(uuid).Should().BeTrue("Generated identifier must be RFC 9562 UUIDv7 compliant");
    }

    [Fact]
    public void New_WithSpecificTimestamp_ShouldEmbedCorrectTimestamp()
    {
        // Arrange
        var targetTime = DateTimeOffset.UtcNow.AddHours(-1);

        // Act
        var uuid = Uuid7.New(targetTime);
        var extractedTime = Uuid7.ExtractTimestamp(uuid);

        // Assert
        extractedTime.ToUnixTimeMilliseconds().Should().Be(targetTime.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void New_SequentialGeneration_ShouldPreserveMonotonicOrdering()
    {
        // Arrange & Act
        var uuid1 = Uuid7.New();
        Thread.Sleep(2);
        var uuid2 = Uuid7.New();

        // Assert
        var time1 = Uuid7.ExtractTimestamp(uuid1);
        var time2 = Uuid7.ExtractTimestamp(uuid2);

        time2.Should().BeOnOrAfter(time1);
        string.CompareOrdinal(uuid1.ToString(), uuid2.ToString()).Should().BeNegative();
    }

    [Fact]
    public void New_MassGeneration_ShouldHaveZeroCollisions()
    {
        // Arrange
        const int count = 10_000;
        var set = new HashSet<Guid>(count);

        // Act
        for (int i = 0; i < count; i++)
        {
            var id = Uuid7.New();
            set.Add(id);
        }

        // Assert
        set.Count.Should().Be(count, "10,000 generated UUIDv7 identifiers must be unique");
    }
}
