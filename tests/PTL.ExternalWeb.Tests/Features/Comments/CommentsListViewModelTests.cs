using PTL.ExternalWeb.Features.Comments;

namespace PTL.ExternalWeb.Tests.Features.Comments;

public class CommentsListViewModelTests
{
    [Fact]
    public void IsFiltered_WhenAnyFilterHasText_IsTrue()
    {
        List<CommentsDistributionListRow> rows =
        [
            new CommentsDistributionListRow(Guid.NewGuid(), "D1a", "S1 BT", "Brucella", new DateTime(2026, 6, 30), "Awaiting comments", "", false)
        ];
        var modelByReference = new CommentsListViewModel(rows, "D1a", null, null, string.Empty, "asc");
        var modelBySchemeCode = new CommentsListViewModel(rows, null, "S1", null, string.Empty, "asc");
        var modelBySchemeName = new CommentsListViewModel(rows, null, null, "Brucella", string.Empty, "asc");

        Assert.True(modelByReference.IsFiltered);
        Assert.True(modelBySchemeCode.IsFiltered);
        Assert.True(modelBySchemeName.IsFiltered);
    }

    [Fact]
    public void IsFiltered_WhenAllFiltersAreNullOrWhitespace_IsFalse()
    {
        var modelWithNull = new CommentsListViewModel([], null, null, null, string.Empty, "asc");
        var modelWithWhitespace = new CommentsListViewModel([], "   ", "", "\t", string.Empty, "asc");

        Assert.False(modelWithNull.IsFiltered);
        Assert.False(modelWithWhitespace.IsFiltered);
    }

    [Fact]
    public void CommentsDistributionListRow_StoresSubmittedFlag()
    {
        var row = new CommentsDistributionListRow(Guid.NewGuid(), "D1a", "S1 BT", "Brucella", new DateTime(2026, 6, 30), "Comments submitted", "Dr Who", true);

        Assert.True(row.IsSubmitted);
    }
}
