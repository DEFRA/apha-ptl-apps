using System.ComponentModel.DataAnnotations;
using PTL.Contracts.Search;

namespace PTL.InternalWeb.Features.Distribution;

// Legacy Distributions/Search.aspx TabContainer1 (no ActiveTabIndex set, so Distribution Number -
// the first tab in the markup - is the default).
public enum DistributionSearchTab
{
    DistributionNumber,
    SampleNumber,
}

// Legacy Distributions/Search.aspx Distribution Number tab ("Use the search box below to identify
// the required Scheme by Distribution Number."). Distribution Number is optional - a blank search
// returns every distribution, exactly as dbo.spgSearchSchemeByDistribution treats ''.
public sealed class DistributionNumberSearchForm
{
    [StringLength(50, ErrorMessage = "Distribution Number must be 50 characters or fewer")]
    [Display(Name = "Distribution Number")]
    public string? DistributionNumber { get; set; }

    public DistributionNumberSearchCriteriaRequest ToCriteria() => new(DistributionNumber);
}

// Legacy Distributions/Search.aspx Sample Number tab ("Use the search box below to identify the
// required Scheme by Sample Number."). Sample Number is mandatory - legacy LoadSampleNumberSearchData
// shows "A Sample Number must be supplied." and does not search when blank.
public sealed class SampleNumberSearchForm
{
    [Required(ErrorMessage = "A Sample Number must be supplied.")]
    [StringLength(50, ErrorMessage = "Sample Number must be 50 characters or fewer")]
    [Display(Name = "Sample Number")]
    public string? SampleNumber { get; set; }

    public SampleNumberSearchCriteriaRequest ToCriteria() => new(SampleNumber);
}

// DistributionNumberResults / SampleNumberResults are null until that tab has been searched (legacy
// showed the "Search Results" panel only after Search was clicked); an empty list means "no results"
// ("There are no Results to Display", legacy GridView EmptyDataTemplate).
public sealed class DistributionSearchViewModel
{
    // Legacy GVsampleNumber.PageSize (Search.aspx.vb Page_Load) - the Sample Number tab's only
    // page size; it has no page-size selector in legacy, so none is offered here either. The
    // Distribution Number tab has no paging in legacy and none is added here.
    public const int SampleNumberPageSize = 15;

    public DistributionSearchTab ActiveTab { get; init; } = DistributionSearchTab.DistributionNumber;

    public DistributionNumberSearchForm DistributionNumber { get; init; } = new();

    public SampleNumberSearchForm SampleNumber { get; init; } = new();

    public IReadOnlyList<DistributionNumberSearchResultResponse>? DistributionNumberResults { get; init; }

    public IReadOnlyList<SampleNumberSearchResultResponse>? SampleNumberResults { get; init; }

    public int SampleNumberTotalCount { get; init; }

    public int SampleNumberPage { get; init; } = 1;
}
