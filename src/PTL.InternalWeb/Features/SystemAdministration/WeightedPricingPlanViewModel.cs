using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SystemAdministration;

// One row of the grid: "No. Per Year" (NumberOfDistributionsOnScheme) plus one weight per
// "No. Chosen" column - mirrors legacy WeightedPricingPlan.aspx.vb's BuildTable(). A null weight
// means no percentage is configured for that (row, column) pair, same as the legacy grid leaving
// the cell blank (GetPricingPercentageString returns an empty string, not "0%").
public sealed class WeightedPricingPlanRowViewModel
{
    public int NumberOfDistributionsOnScheme { get; set; }

    public Dictionary<int, int?> WeightByDistributionsChosen { get; set; } = [];
}

public sealed class WeightedPricingPlanViewModel
{
    public int? SelectedYearId { get; set; }

    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    // Column headers, sorted - shared across every row so the grid always lines up.
    public List<int> DistributionsChosenColumns { get; set; } = [];

    public List<WeightedPricingPlanRowViewModel> Rows { get; set; } = [];

    // True only when there is no financial year with any percentages configured at all - shows
    // the legacy "No weighted pricing percentages have been entered. Please contact SFW." banner
    // instead of the grid.
    public bool HasNoYears { get; set; }

    public bool CanRenew { get; set; }

    public string? NextYearLabel { get; set; }

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}
