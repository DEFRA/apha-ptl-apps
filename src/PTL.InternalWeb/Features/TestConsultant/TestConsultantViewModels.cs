using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.TestConsultant;

// Test Consultant menu (legacy Test Consultant/MenuTestConsultants.aspx): the year drop-down and the distributions of
// that year grouped by month.
public sealed class TestConsultantMenuViewModel
{
    public required IReadOnlyList<int> Years { get; init; }
    public required int SelectedYear { get; init; }
    public required IReadOnlyList<TestConsultantMonthGroup> Months { get; init; }

    public static TestConsultantMenuViewModel From(TestConsultantDistributionsResponse response, int? requestedYear)
    {
        ArgumentNullException.ThrowIfNull(response);

        // Legacy filled the drop-down oldest first and selected the last (latest) year.
        var years = response.Distributions.Select(d => d.YearId).Distinct().Order().ToList();
        var selected = requestedYear is { } year && years.Contains(year) ? year : years.LastOrDefault();

        return new TestConsultantMenuViewModel
        {
            Years = years,
            SelectedYear = selected,
            Months =
            [
                .. response.Distributions
                    .Where(d => d.YearId == selected)
                    .GroupBy(d => d.MonthId)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new TestConsultantMonthGroup(g.Key, MonthName(g.Key, selected), [.. g])),
            ],
        };
    }

    private static string MonthName(int monthId, int yearId) =>
        $"{CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetMonthName(Math.Clamp(monthId, 1, 12))} {yearId.ToString(CultureInfo.InvariantCulture)}";
}

public sealed record TestConsultantMonthGroup(int MonthId, string Title, IReadOnlyList<TestConsultantDistributionResponse> Distributions);

// Test Consultant Tabulation (legacy Test Consultant/Tabulation.aspx). One form holds the sample filter and the comments
// editor, so "Draw tabulations" keeps what has been typed, as the legacy postback did.
public sealed class TestConsultantTabulationViewModel
{
    // Display-only: set from the route and the API, never from the posted form.
    [BindNever]
    public Guid MonthlyDistributionSchemeId { get; set; }

    [BindNever]
    public TestConsultantTabulationResponse? Tabulation { get; set; }

    // Every sample of the distribution, and the ones left ticked. The filter only applies once the form has been drawn.
    public List<string> AllSamples { get; set; } = [];
    public List<string> ShownSamples { get; set; } = [];
    public bool ShowMethodItems { get; set; } = true;
    public bool IsFiltered { get; set; }

    // Legacy ValidatorCommentsRequired / ValidatorCommentsRegEx.
    [Required(ErrorMessage = "Comments required.")]
    [StringLength(50000, ErrorMessage = "Comments must not exceed 50,000 characters. Note that rich text such as font changes and bullet points will artificially count towards this character limit.")]
    public string? Comments { get; set; }

    public TestConsultantTabulationFilter ToFilter() =>
        IsFiltered
            ? new TestConsultantTabulationFilter([.. AllSamples.Except(ShownSamples, StringComparer.Ordinal)], !ShowMethodItems)
            : TestConsultantTabulationFilter.None;

    // Fills the filter from what the API reports, keeping the Test Consultant's ticks once the form has been drawn.
    public void Display(Guid monthlyDistributionSchemeId, TestConsultantTabulationResponse tabulation)
    {
        ArgumentNullException.ThrowIfNull(tabulation);

        MonthlyDistributionSchemeId = monthlyDistributionSchemeId;
        Tabulation = tabulation;
        ShownSamples = IsFiltered
            ? [.. tabulation.SampleNumbers.Intersect(ShownSamples, StringComparer.Ordinal)]
            : [.. tabulation.SampleNumbers];
        AllSamples = [.. tabulation.SampleNumbers];
        IsFiltered = true;
    }
}
