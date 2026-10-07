using PTL.InternalWeb.Features.Participant;

namespace PTL.InternalWeb.Tests.Features.Participant;

public class PendingParticipantUpdateComparisonRowTests
{
    [Fact]
    public void HasChanged_SameValueIgnoringSurroundingWhitespace_IsFalse()
    {
        var row = new PendingParticipantUpdateComparisonRow("Contact Name", "Alice Example", "  Alice Example  ");

        Assert.False(row.HasChanged);
    }

    [Fact]
    public void HasChanged_DifferentValues_IsTrue()
    {
        var row = new PendingParticipantUpdateComparisonRow("Contact Name", "Alice Example", "Bob Example");

        Assert.True(row.HasChanged);
    }

    [Fact]
    public void HasChanged_BothValuesNull_IsFalse()
    {
        var row = new PendingParticipantUpdateComparisonRow("Contact Name", null!, null!);

        Assert.False(row.HasChanged);
    }

    [Fact]
    public void HasChanged_OneValueNull_IsTrue()
    {
        var row = new PendingParticipantUpdateComparisonRow("Contact Name", null!, "Bob Example");

        Assert.True(row.HasChanged);
    }
}
