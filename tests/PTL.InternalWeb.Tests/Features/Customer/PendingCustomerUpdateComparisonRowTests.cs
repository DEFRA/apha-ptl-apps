using PTL.InternalWeb.Features.Customer;

namespace PTL.InternalWeb.Tests.Features.Customer;

public class PendingCustomerUpdateComparisonRowTests
{
    [Fact]
    public void HasChanged_SameValueIgnoringSurroundingWhitespace_IsFalse()
    {
        var row = new PendingCustomerUpdateComparisonRow("Contact Name", "Alice Example", "  Alice Example  ");

        Assert.False(row.HasChanged);
    }

    [Fact]
    public void HasChanged_DifferentValues_IsTrue()
    {
        var row = new PendingCustomerUpdateComparisonRow("Contact Name", "Alice Example", "Bob Example");

        Assert.True(row.HasChanged);
    }

    [Fact]
    public void HasChanged_BothValuesNull_IsFalse()
    {
        var row = new PendingCustomerUpdateComparisonRow("Contact Name", null!, null!);

        Assert.False(row.HasChanged);
    }

    [Fact]
    public void HasChanged_OneValueNull_IsTrue()
    {
        var row = new PendingCustomerUpdateComparisonRow("Contact Name", null!, "Bob Example");

        Assert.True(row.HasChanged);
    }
}
