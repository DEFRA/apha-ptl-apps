using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.Distribution;

// One calendar month row on the Distribution Dashboard (legacy MenuDistributions.aspx GridView1).
public sealed record DistributionDashboardMonthRowViewModel(
    int YearId,
    int MonthId,
    string MonthDescription,
    bool IsInitialised,
    bool IsInitialisable,
    int SchedulingCompletePercentage,
    int PreparationCompletePercentage,
    int PackagingCompletePercentage,
    int ResultsCompletePercentage,
    int TabulationsCompletePercentage);

public sealed class DistributionDashboardViewModel
{
    public int SelectedYearId { get; init; }

    public IReadOnlyList<SelectListItem> YearOptions { get; init; } = [];

    public IReadOnlyList<DistributionDashboardMonthRowViewModel> Months { get; init; } = [];
}
