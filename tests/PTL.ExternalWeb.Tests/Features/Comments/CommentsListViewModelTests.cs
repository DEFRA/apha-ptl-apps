using PTL.ExternalWeb.Features.Comments;

namespace PTL.ExternalWeb.Tests.Features.Comments;

public class CommentsListViewModelTests
{
    [Fact]
    public void IsFiltered_WhenAnyFilterHasText_IsTrue()
    {
        List<CommentsDistributionListRow> rows =
        [
            new CommentsDistributionListRow(Guid.NewGuid(), "D1a", "S1 BT", "Brucella", new DateTime(2026, 6, 30), "Awaiting comments")
        ];
        var modelByReference = new CommentsListViewModel(rows, "D1a", null, null);
        var modelBySchemeCode = new CommentsListViewModel(rows, null, "S1", null);
        var modelBySchemeName = new CommentsListViewModel(rows, null, null, "Brucella");

        Assert.True(modelByReference.IsFiltered);
        Assert.True(modelBySchemeCode.IsFiltered);
        Assert.True(modelBySchemeName.IsFiltered);
    }

    [Fact]
    public void IsFiltered_WhenAllFiltersAreNullOrWhitespace_IsFalse()
    {
        var modelWithNull = new CommentsListViewModel([], null, null, null);
        var modelWithWhitespace = new CommentsListViewModel([], "   ", "", "\t");

        Assert.False(modelWithNull.IsFiltered);
        Assert.False(modelWithWhitespace.IsFiltered);
    }
}
