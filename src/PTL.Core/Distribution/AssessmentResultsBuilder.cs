using System.Globalization;
using PTL.Contracts.Distribution;

namespace PTL.Core.Distribution;

// Ports legacy AssessmentScoreInfoCollection/AssessmentScoreInfo and the titles set up by
// AssessmentResultsChart.ascx.vb (SetUpChartTitles / SetUpBarChart).
public static class AssessmentResultsBuilder
{
    // Legacy bar order: scores 1-5, then Not Submitted (0), then Not In Repertoire (-1).
    private static readonly (int Score, string Label)[] Bars =
    [
        (1, "1 Fail"),
        (2, "2 Borderline Fail"),
        (3, "3 Pass"),
        (4, "4 Good"),
        (5, "5 Excellent"),
        (0, "Not Submitted"),
        (-1, "Not In Repertoire"),
    ];

    public static AssessmentResultsResponse Build(AssessmentScoreSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var frequencies = Bars.ToDictionary(b => b.Score, _ => 0);
        var sumScore = 0;
        var assessedCount = 0;

        foreach (var receipt in source.Receipts)
        {
            var submitted = receipt.SubmissionDate is not null;
            var notSubmitted = receipt.NonSubmissionDate is not null;
            var notCapable = receipt.NonCapableDate is not null;

            if (submitted && !notSubmitted && !notCapable)
            {
                // Submitted but not yet assessed rows are not counted; legacy failed on an
                // out-of-range score, which is skipped here instead.
                if (receipt.AssessmentDate is not null && receipt.Score is >= 1 and <= 5)
                {
                    frequencies[receipt.Score]++;
                    assessedCount++;
                    sumScore += receipt.Score;
                }
            }
            else if (notSubmitted && !submitted && !notCapable)
            {
                frequencies[0]++;
            }
            else if (notCapable && !submitted && !notSubmitted)
            {
                frequencies[-1]++;
            }
        }

        // Legacy Math.Round(sum / count, 1) - "NaN" when nothing has been assessed.
        var average = Math.Round((double)sumScore / assessedCount, 1).ToString(CultureInfo.GetCultureInfo("en-GB"));

        return new AssessmentResultsResponse(
            source.AssessmentSubmitted is not null,
            [
                "Bar chart shows the frequency of assessment scores",
                "Scheme: " + source.SchemeName,
                "Distribution: " + source.Distribution,
                "Sample number(s): " + string.Join(", ", source.SampleNumbers).Trim(),
                "Tabulation: " + source.TabulationName,
                "Test name: " + source.TestName,
                "Number of samples assessed: " + assessedCount.ToString(CultureInfo.InvariantCulture),
                "Average score: " + average,
            ],
            "Assessment Score",
            "Frequency",
            [.. Bars.Select(b => new AssessmentScoreFrequencyResponse(b.Score, b.Label, frequencies[b.Score]))]);
    }
}
