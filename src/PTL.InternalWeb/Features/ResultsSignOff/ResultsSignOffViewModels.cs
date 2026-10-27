using System.Globalization;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.ResultsSignOff;

// Results Sign-off queue (legacy Results Sign-off/SignOff.aspx): the distribution schemes with tabulations awaiting final
// approval, grouped by month, oldest month first. Every scheme starts collapsed, as legacy's tree did.
public sealed class ResultsSignOffQueueViewModel
{
    // Legacy linked each View to Results Sign-off/Tabulation.aspx?TabulationId=... (the review page).
    public const string TabulationPath = "/Results%20Sign-off/Tabulation";

    public required IReadOnlyList<ResultsSignOffMonthGroup> Months { get; init; }

    public static ResultsSignOffQueueViewModel From(ResultsSignOffQueueResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new ResultsSignOffQueueViewModel
        {
            Months = [.. response.Months.Select(m => new ResultsSignOffMonthGroup(MonthTitle(m.MonthId, m.YearId), m.Schemes))],
        };
    }

    public static string TabulationUrl(Guid tabulationId) => $"{TabulationPath}?tabulationId={tabulationId}";

    private static string MonthTitle(int monthId, int yearId) =>
        $"{CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetMonthName(Math.Clamp(monthId, 1, 12))} {yearId.ToString(CultureInfo.InvariantCulture)}";
}

public sealed record ResultsSignOffMonthGroup(string Title, IReadOnlyList<ResultsSignOffSchemeResponse> Schemes);

// Results Sign-Off Tabulation (legacy Results Sign-off/Tabulation.aspx): the final draft the reviewer authorises or rejects.
public sealed class ResultsSignOffReviewViewModel
{
    public required Guid TabulationId { get; init; }
    public required ResultsSignOffReviewResponse Review { get; init; }
    public required ResultsSignOffDraw Draw { get; init; }

    public TabulationReportResponse Report => Review.Report;
}

// The check boxes above the tables (legacy HiddenColumnBlocks), read from the query string of the "Draw tabulation" form: every
// sample of the distribution and the ones left ticked. The filter only applies once the form has been drawn.
public sealed class ResultsSignOffDraw
{
    public List<string> AllSamples { get; set; } = [];
    public List<string> ShownSamples { get; set; } = [];
    public bool ShowMethodItems { get; set; } = true;
    public bool? IsFiltered { get; set; }

    public TestConsultantTabulationFilter ToFilter() =>
        IsFiltered == true
            ? new TestConsultantTabulationFilter([.. AllSamples.Except(ShownSamples, StringComparer.Ordinal)], !ShowMethodItems)
            : TestConsultantTabulationFilter.None;

    // Fills the check boxes from what the API reports, keeping the reviewer's ticks once the form has been drawn.
    public void Display(IReadOnlyList<string> sampleNumbers)
    {
        ArgumentNullException.ThrowIfNull(sampleNumbers);

        ShownSamples = IsFiltered == true
            ? [.. sampleNumbers.Intersect(ShownSamples, StringComparer.Ordinal)]
            : [.. sampleNumbers];
        AllSamples = [.. sampleNumbers];
        IsFiltered = true;
    }
}

// Assessment Results (legacy Results Sign-off/AssessmentResults.aspx): the score chart of one test of the tabulation.
public sealed class ResultsSignOffAssessmentViewModel
{
    public required Guid TabulationId { get; init; }
    public required AssessmentResultsResponse Results { get; init; }
}
