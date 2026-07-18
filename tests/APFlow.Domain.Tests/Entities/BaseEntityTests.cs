using APFlow.Domain.Entities;
using Xunit;

namespace APFlow.Domain.Tests.Entities;

public class BaseEntityTests
{
    private sealed class TestEntity : BaseEntity
    {
    }

    [Fact]
    public void Id_IsAssigned_ImmediatelyAtConstruction()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void Id_IsDifferent_AcrossInstances()
    {
        var first = new TestEntity();
        var second = new TestEntity();

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Id_IsTimeOrdered_LaterInstanceSortsAfterEarlierOne()
    {
        var first = new TestEntity();
        var second = new TestEntity();

        // IMPORTANT: .NET's default Guid.CompareTo does NOT reliably preserve UUID v7
        // chronological order - Guid's internal field layout (native-endian int/short
        // groups) differs from the RFC 9562 big-endian byte order that actually encodes
        // the timestamp. Comparing via ToByteArray(bigEndian: true) is the correct way
        // to verify sortability; relying on Guid.CompareTo/operator< directly would be
        // wrong (confirmed empirically: it produces inconsistent results for GUIDs
        // created within the same millisecond). This also means: don't sort entities by
        // Id in LINQ/SQL and expect chronological order - that guarantee only holds for
        // the correctly-ordered raw bytes, not .NET's or SQL Server's default comparers.
        // Only the first 6 bytes (48-bit Unix ms timestamp, RFC 9562 §5.7) are ordering-
        // guaranteed - the remaining bytes are random and not monotonic, so two ids
        // created within the same millisecond can legitimately have a "later" instance
        // whose random tail compares less than an "earlier" one's. Comparing the full 16
        // bytes made this test flaky (confirmed empirically: intermittent failures across
        // repeated runs).
        var firstTimestamp = first.Id.ToByteArray(bigEndian: true).AsSpan(0, 6);
        var secondTimestamp = second.Id.ToByteArray(bigEndian: true).AsSpan(0, 6);

        Assert.True(secondTimestamp.SequenceCompareTo(firstTimestamp) >= 0);
    }
}
