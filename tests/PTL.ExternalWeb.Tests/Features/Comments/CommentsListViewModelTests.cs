using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Comments;

namespace PTL.ExternalWeb.Tests.Features.Comments;

public class CommentsListViewModelTests
{
    [Fact]
    public void IsFiltered_WhenSearchHasText_IsTrue()
    {
        var model = new CommentsListViewModel(
            [new TestConsultantDistributionResponse(Guid.NewGuid(), 2026, 6, "D1", new DateTime(2026, 6, 30), false, "Awaiting comments")],
            "Brucella");

        Assert.True(model.IsFiltered);
    }

    [Fact]
    public void IsFiltered_WhenSearchIsNullOrWhitespace_IsFalse()
    {
        var modelWithNull = new CommentsListViewModel([], null);
        var modelWithWhitespace = new CommentsListViewModel([], "   ");

        Assert.False(modelWithNull.IsFiltered);
        Assert.False(modelWithWhitespace.IsFiltered);
    }
}
