using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.InternalWeb.Features.Assessor;

// Assessor menu (legacy Assessor/MenuAssessor.aspx): the year drop-down and the months of that year with their distributions
// and tests. Legacy opened every month; so does the page.
public sealed class AssessorMenuViewModel
{
    public required IReadOnlyList<int> Years { get; init; }
    public required int SelectedYear { get; init; }
    public required IReadOnlyList<AssessorMonthGroup> Months { get; init; }

    public static AssessorMenuViewModel From(AssessorMenuResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new AssessorMenuViewModel
        {
            Years = response.Years,
            SelectedYear = response.SelectedYear,
            Months = [.. response.Months.Select(m => new AssessorMonthGroup(AssessorService.YearMonthTitle(m.MonthId, m.YearId), m.Distributions))],
        };
    }
}

public sealed record AssessorMonthGroup(string Title, IReadOnlyList<AssessorDistributionResponse> Distributions);

// Assessment Items (legacy Assessor/AssessmentItemList.aspx). Year is the one the menu was showing, so Back returns to it.
public sealed class AssessorItemListViewModel
{
    // Legacy linked each View/Edit to Assessor/AssessmentEntry.aspx?SampleTestId=...&ParticipantId=... (the Assessment Entry page).
    // TestId and Year only let the page link back to this one (its breadcrumb, Cancel and Back).
    public const string EntryPath = "/Assessor/AssessmentEntry";

    public required AssessorItemListResponse List { get; init; }
    public int? Year { get; init; }

    public static string EntryUrl(AssessorItemResponse item, Guid testId, int? year)
    {
        ArgumentNullException.ThrowIfNull(item);

        var url = $"{EntryPath}?testId={testId}&sampleTestId={item.SampleTestId}&participantId={item.ParticipantId}";
        return year is { } value ? $"{url}&year={value.ToString(CultureInfo.InvariantCulture)}" : url;
    }

    // Legacy: "Assessment was submitted successfully for this test by {name} on {date}".
    public string? SubmittedMessage =>
        List is { IsAssessmentSubmitted: true, SubmittedDate: { } date }
            ? $"Assessment was submitted successfully for this test by {List.SubmittedBy} on {date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}"
            : null;
}

// Assessment Entry (legacy Assessor/AssessmentEntry.aspx). One form holds the comment, the overall score and every category's
// score and ticked criteria, so Apply keeps what has been entered. The posted values win over the saved ones when the form is
// shown again with errors.
public sealed class AssessorEntryViewModel
{
    public const string SaveCommand = "save";
    public const string ApplyCommand = "apply";

    // Legacy overall score drop-down: "Please select...", then 1 - Fail to 5 - Excellent.
    public static IReadOnlyList<SelectListItem> OverallScoreOptions { get; } =
    [
        new("1 - Fail", "1"),
        new("2 - Borderline Fail", "2"),
        new("3 - Pass", "3"),
        new("4 - Good", "4"),
        new("5 - Excellent", "5"),
    ];

    public static IReadOnlyList<SelectListItem> CategoryScoreOptions { get; } = [.. Enumerable.Range(1, 5).Select(score => new SelectListItem(score.ToString(CultureInfo.InvariantCulture), score.ToString(CultureInfo.InvariantCulture)))];

    // Display-only: set from the API, never from the posted form.
    [BindNever]
    public AssessorEntryResponse? Entry { get; set; }

    [Required]
    public Guid? SampleTestId { get; set; }

    [Required]
    public Guid? ParticipantId { get; set; }

    // The year the menu was showing, so Cancel and Back return to it.
    public int? Year { get; set; }

    // Legacy ValidatorAssessmentCommentMaxLength.
    [StringLength(AssessorService.MaxCommentLength, ErrorMessage = AssessorService.CommentTooLongMessage)]
    public string? Comment { get; set; }

    // Legacy rfvOverallScore: "Please select..." is not a score.
    [Required(ErrorMessage = AssessorService.OverallScoreRequiredMessage)]
    [Range(AssessorService.MinScore, AssessorService.MaxScore, ErrorMessage = AssessorService.OverallScoreRequiredMessage)]
    public int? OverallScore { get; set; }

    public List<AssessorCategoryInput> Categories { get; set; } = [];

    // The saved values, for the first time the page is shown.
    public static AssessorEntryViewModel From(AssessorEntryResponse entry, int? year)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new AssessorEntryViewModel
        {
            Entry = entry,
            SampleTestId = entry.SampleTestId,
            ParticipantId = entry.ParticipantId,
            Year = year,
            Comment = entry.Comment,
            OverallScore = entry.OverallScore == 0 ? null : entry.OverallScore,
            Categories =
            [
                .. entry.Categories.Select(c => new AssessorCategoryInput
                {
                    CategoryItemId = c.CategoryItemId,
                    Score = c.Score == 0 ? null : c.Score,
                    CriterionItemIds = [.. c.Criteria.Where(i => i.IsSelected).Select(i => i.CriterionItemId)],
                }),
            ],
        };
    }

    // Keeps the posted values and lines the categories up with the test's, in the test's order.
    public void Display(AssessorEntryResponse entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entry = entry;
        var posted = Categories.GroupBy(c => c.CategoryItemId).ToDictionary(g => g.Key, g => g.First());
        Categories = [.. entry.Categories.Select(c => posted.GetValueOrDefault(c.CategoryItemId) ?? new AssessorCategoryInput { CategoryItemId = c.CategoryItemId })];
    }

    public AssessorEntrySaveRequest ToRequest() =>
        new(
            Comment,
            OverallScore ?? 0,
            [.. Categories.Select(c => new AssessorCategorySaveRequest(c.CategoryItemId, c.Score ?? 0, c.CriterionItemIds))]);
}

public sealed class AssessorCategoryInput
{
    public Guid CategoryItemId { get; set; }

    // Legacy rfv on each category's drop-down.
    [Required(ErrorMessage = AssessorService.CategoryScoreRequiredMessage)]
    [Range(AssessorService.MinScore, AssessorService.MaxScore, ErrorMessage = AssessorService.CategoryScoreRequiredMessage)]
    public int? Score { get; set; }

    public List<Guid> CriterionItemIds { get; set; } = [];
}
