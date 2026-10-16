using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Legacy AssessmentScoreInfoCollection + AssessmentResultsChart.ascx.vb SetUpChartTitles/SetUpBarChart.
public class AssessmentResultsBuilderTests
{
    private static readonly DateTime Day = new(2026, 5, 10);

    private static AssessmentScoreReceipt Assessed(int score) => new(Day, null, null, score, Day);

    private static AssessmentScoreSource Source(DateTime? assessmentSubmitted, params AssessmentScoreReceipt[] receipts) =>
        new("Summary", "Brucella", "D1a/S1", "Culture", assessmentSubmitted, ["S1 ", "S2"], receipts);

    [Fact]
    public void Build_NullSource_Throws() =>
        Assert.Throws<ArgumentNullException>(() => AssessmentResultsBuilder.Build(null!));

    [Fact]
    public void Build_CountsOnlySubmittedAssessedScoresOneToFiveAndTheExclusiveNonSubmissionStates()
    {
        var source = Source(
            Day,
            Assessed(1), Assessed(3), Assessed(5), Assessed(5),
            new AssessmentScoreReceipt(Day, null, null, 4, null),
            Assessed(7),
            new AssessmentScoreReceipt(null, Day, null, 0, null),
            new AssessmentScoreReceipt(null, Day, null, 0, null),
            new AssessmentScoreReceipt(null, null, Day, 0, null),
            new AssessmentScoreReceipt(Day, Day, null, 2, Day),
            new AssessmentScoreReceipt(null, Day, Day, 0, null),
            new AssessmentScoreReceipt(null, null, null, 0, null));

        var result = AssessmentResultsBuilder.Build(source);

        Assert.Equal(
            [(1, "1 Fail", 1), (2, "2 Borderline Fail", 0), (3, "3 Pass", 1), (4, "4 Good", 0), (5, "5 Excellent", 2), (0, "Not Submitted", 2), (-1, "Not In Repertoire", 1)],
            result.Scores.Select(s => (s.Score, s.Label, s.Frequency)));
        Assert.Equal("Number of samples assessed: 4", result.Titles[6]);
        Assert.Equal("Average score: 3.5", result.Titles[7]);
    }

    [Fact]
    public void Build_TitlesAxesAndSubmittedFlag_FollowLegacyChartSetUp()
    {
        var result = AssessmentResultsBuilder.Build(Source(Day, Assessed(1), Assessed(2), Assessed(2)));

        Assert.True(result.IsAssessmentSubmitted);
        Assert.Equal(
            [
                "Bar chart shows the frequency of assessment scores",
                "Scheme: Brucella",
                "Distribution: D1a/S1",
                "Sample number(s): S1 , S2",
                "Tabulation: Summary",
                "Test name: Culture",
                "Number of samples assessed: 3",
                "Average score: 1.7",
            ],
            result.Titles);
        Assert.Equal(("Assessment Score", "Frequency"), (result.XAxisTitle, result.YAxisTitle));
    }

    [Fact]
    public void Build_NothingAssessed_AverageIsNaNAndAssessmentNotSubmitted()
    {
        var result = AssessmentResultsBuilder.Build(Source(null));

        Assert.False(result.IsAssessmentSubmitted);
        Assert.Equal("Number of samples assessed: 0", result.Titles[6]);
        Assert.Equal("Average score: NaN", result.Titles[7]);
        Assert.All(result.Scores, s => Assert.Equal(0, s.Frequency));
        Assert.Equal(7, result.Scores.Count);
    }
}
