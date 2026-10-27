using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.ResultsSignOff;

namespace PTL.InternalWeb.Tests.Features.ResultsSignOff;

public class ResultsSignOffQueueViewModelTests
{
    private static ResultsSignOffSchemeResponse Scheme(string title = "17208/BA/PT0074 PM: Pasteurella") =>
        new(Guid.NewGuid(), title, new DateTime(2026, 7, 21), "Jane Smith", "Ann Assessor", []);

    [Fact]
    public void From_NullResponse_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ResultsSignOffQueueViewModel.From(null!));

    [Fact]
    public void From_NothingAwaiting_HasNoMonths() =>
        Assert.Empty(ResultsSignOffQueueViewModel.From(new ResultsSignOffQueueResponse([])).Months);

    [Fact]
    public void From_TitlesEachMonthWithItsNameAndYearAndKeepsTheOrder()
    {
        var model = ResultsSignOffQueueViewModel.From(new ResultsSignOffQueueResponse(
        [
            new ResultsSignOffMonthResponse(2026, 5, [Scheme()]),
            new ResultsSignOffMonthResponse(2026, 12, [Scheme(), Scheme()]),
        ]));

        Assert.Equal(["May 2026", "December 2026"], model.Months.Select(m => m.Title));
        Assert.Equal([1, 2], model.Months.Select(m => m.Schemes.Count));
    }

    [Theory]
    [InlineData(0, "January 2026")]
    [InlineData(13, "December 2026")]
    public void From_ClampsAnOutOfRangeMonth(int monthId, string expected) =>
        Assert.Equal(expected, ResultsSignOffQueueViewModel.From(new ResultsSignOffQueueResponse([new ResultsSignOffMonthResponse(2026, monthId, [])])).Months[0].Title);

    [Fact]
    public void TabulationUrl_PointsAtTheReviewPageWithTheTabulationId()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal("/Results%20Sign-off/Tabulation?tabulationId=22222222-2222-2222-2222-222222222222", ResultsSignOffQueueViewModel.TabulationUrl(id));
    }
}
