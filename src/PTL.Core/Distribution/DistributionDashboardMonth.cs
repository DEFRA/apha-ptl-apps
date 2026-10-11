namespace PTL.Core.Distribution;

// Ports MonthlyDistributionInfo.vb's read-only derived properties verbatim, including each
// getter's own division-by-zero fallback - these are NOT all the same rule, don't unify them.
public static class DistributionMonthSummaryExtensions
{
    public static decimal SchedulingCompletePercentage(this DistributionMonthSummaryEntity summary) =>
        summary.NumberOfSchemes == 0 ? 100m : (decimal)summary.NumberOfSampleNumbersDefined / summary.NumberOfSchemes * 100m;

    public static decimal PreparationCompletePercentage(this DistributionMonthSummaryEntity summary) =>
        summary.NumberOfSchemes == 0 ? 100m : (decimal)summary.NumberOfPrepComplete / summary.NumberOfSchemes * 100m;

    public static decimal PackagingCompletePercentage(this DistributionMonthSummaryEntity summary) =>
        summary.NumberOfParticipants == 0 ? 100m : (decimal)summary.NumberOfPackagingComplete / summary.NumberOfParticipants * 100m;

    public static decimal ResultsCompletePercentage(this DistributionMonthSummaryEntity summary) =>
        summary.NumberOfParticipants == 0 ? 100m : (decimal)summary.NumberOfResultsEntered / summary.NumberOfParticipants * 100m;

    public static decimal TabulationsCompletePercentage(this DistributionMonthSummaryEntity summary)
    {
        if (summary.NumberOfTabulations == 0)
        {
            return summary.NumberOfSchemes == 0 ? 100m : 0m;
        }

        return (decimal)summary.NumberOfCompleteTabulations / summary.NumberOfTabulations * 100m;
    }
}

// One row of the dashboard: either a real, already-initialised month (MonthlyDistributionId set,
// IsInitialisable always false - legacy never re-shows Initialise for an existing month) or a
// not-yet-initialised placeholder (MonthlyDistributionId null, percentages all zero).
public sealed record DistributionDashboardMonth(
    int YearId,
    int MonthId,
    Guid? MonthlyDistributionId,
    bool IsInitialisable,
    decimal SchedulingCompletePercentage,
    decimal PreparationCompletePercentage,
    decimal PackagingCompletePercentage,
    decimal ResultsCompletePercentage,
    decimal TabulationsCompletePercentage);

// Ports MonthlyDistributionInfoCollection.vb's Fetch(cn, criteria) in-memory financial-year
// (April -> March) building logic verbatim, including the "sequential initialisation" rule: a
// month only becomes initialisable once the immediately preceding month has itself been
// initialised (or, if nothing has ever been initialised, every month in the year is immediately
// initialisable).
public static class DistributionDashboardBuilder
{
    public static IReadOnlyList<DistributionDashboardMonth> Build(
        IReadOnlyList<DistributionMonthSummaryEntity> allMonths,
        int financialYearId)
    {
        var byAccumulatedMonth = new Dictionary<int, DistributionMonthSummaryEntity>();
        foreach (var month in allMonths)
        {
            byAccumulatedMonth[(month.YearId * 12) + month.MonthId] = month;
        }

        var result = new List<DistributionDashboardMonth>(12);

        if (byAccumulatedMonth.Count == 0)
        {
            // Legacy: "if there are no MDs yet, let them select anything to initialise".
            for (var n = 4; n <= 15; n++)
            {
                var (yearId, monthId) = ToCalendarMonth(financialYearId, n);
                result.Add(Placeholder(yearId, monthId, isInitialisable: true));
            }

            return result;
        }

        for (var n = 4; n <= 15; n++)
        {
            var accumulatedMonth = (financialYearId * 12) + n;
            var (yearId, monthId) = ToCalendarMonth(financialYearId, n);

            if (byAccumulatedMonth.TryGetValue(accumulatedMonth, out var existing))
            {
                result.Add(new DistributionDashboardMonth(
                    yearId,
                    monthId,
                    existing.MonthlyDistributionId,
                    IsInitialisable: false,
                    existing.SchedulingCompletePercentage(),
                    existing.PreparationCompletePercentage(),
                    existing.PackagingCompletePercentage(),
                    existing.ResultsCompletePercentage(),
                    existing.TabulationsCompletePercentage()));
            }
            else if (byAccumulatedMonth.ContainsKey(accumulatedMonth - 1))
            {
                result.Add(Placeholder(yearId, monthId, isInitialisable: true));
            }
            else
            {
                result.Add(Placeholder(yearId, monthId, isInitialisable: false));
            }
        }

        return result;
    }

    private static DistributionDashboardMonth Placeholder(int yearId, int monthId, bool isInitialisable) =>
        new(yearId, monthId, MonthlyDistributionId: null, isInitialisable, 0, 0, 0, 0, 0);

    private static (int YearId, int MonthId) ToCalendarMonth(int financialYearId, int n) =>
        n > 12 ? (financialYearId + 1, n - 12) : (financialYearId, n);
}
