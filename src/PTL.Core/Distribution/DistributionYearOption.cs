using System.Globalization;

namespace PTL.Core.Distribution;

// A selectable financial year (April -> March) for the dashboard's Year dropdown. Ports
// DistributionYearInfo.vb's Text property verbatim: e.g. YearId 2024 -> "2024/25".
public sealed record DistributionYearOption(int YearId)
{
    public string Label => $"{YearId.ToString(CultureInfo.InvariantCulture)}/{(YearId + 1).ToString(CultureInfo.InvariantCulture)[^2..]}";
}

// Ports DistributionYearInfoCollection.vb's DataPortal_Fetch verbatim: existing distribution
// years (most recent first, as returned by the stored procedure) plus the current financial year
// and a "look ahead" financial year, each added only if not already present.
public static class DistributionYearOptionsBuilder
{
    private const int DefaultLookAheadMonths = 12;

    public static IReadOnlyList<DistributionYearOption> Build(
        IReadOnlyList<DistributionMonthYearEntity> distributionYears,
        DateTime now,
        int lookAheadMonths = DefaultLookAheadMonths)
    {
        var seen = new HashSet<int>();
        var result = new List<DistributionYearOption>();

        foreach (var row in distributionYears)
        {
            var financialYearId = ToFinancialYearId(row.YearId, row.MonthId);
            if (seen.Add(financialYearId))
            {
                result.Add(new DistributionYearOption(financialYearId));
            }
        }

        var currentFinancialYearId = GetDefaultFinancialYearId(now);
        if (seen.Add(currentFinancialYearId))
        {
            result.Add(new DistributionYearOption(currentFinancialYearId));
        }

        var future = now.AddMonths(lookAheadMonths);
        var futureFinancialYearId = GetDefaultFinancialYearId(future);
        if (seen.Add(futureFinancialYearId))
        {
            result.Add(new DistributionYearOption(futureFinancialYearId));
        }

        return result;
    }

    // Legacy's repeated "if current month < 4 then year -= 1" rule (financial year runs Apr-Mar).
    public static int GetDefaultFinancialYearId(DateTime date) => ToFinancialYearId(date.Year, date.Month);

    private static int ToFinancialYearId(int calendarYearId, int calendarMonthId) =>
        calendarMonthId < 4 ? calendarYearId - 1 : calendarYearId;
}
