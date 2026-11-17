using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Tabulation;

namespace PTL.ExternalWeb.Tests.Features.Tabulation;

public class AssessmentResultsPageViewModelTests
{
    private static AssessmentResultsPageViewModel Model(params string[] titles) =>
        new(Guid.NewGuid(), Guid.NewGuid(), new AssessmentResultsResponse(true, titles, "Assessment Score", "Frequency", []));

    [Fact]
    public void Details_DropsTheIntroSentenceAndSplitsOnTheFirstSeparatorOnly()
    {
        var model = Model("Intro", "Scheme: Brucella: serology", " ", "Average score: 3.5");

        Assert.Equal([("Scheme", "Brucella: serology"), ("Average score", "3.5")], model.Details);
    }

    [Fact]
    public void Details_CallsTheTabulationAReport()
    {
        var model = Model("Intro", "Tabulation: Summary A");

        Assert.Equal([("Report", "Summary A")], model.Details);
    }

    [Fact]
    public void Details_LineWithoutSeparator_KeepsTheTextWithNoLabel()
    {
        var model = Model("Intro", "Plain text");

        Assert.Equal([(string.Empty, "Plain text")], model.Details);
    }
}
