namespace PTL.Contracts.Distribution;

// One row of the Distribution Dashboard (legacy MenuDistributions.aspx GridView1) - a single
// calendar month within the selected financial year. MonthlyDistributionId is null until the
// month has been initialised (legacy: Guid.Empty).
public sealed record DistributionDashboardMonthResponse(
    int YearId,
    int MonthId,
    string MonthDescription,
    Guid? MonthlyDistributionId,
    bool IsInitialisable,
    decimal SchedulingCompletePercentage,
    decimal PreparationCompletePercentage,
    decimal PackagingCompletePercentage,
    decimal ResultsCompletePercentage,
    decimal TabulationsCompletePercentage);

// A selectable financial year (April -> March) for the dashboard's Year dropdown.
public sealed record DistributionYearOptionResponse(int YearId, string Label);
