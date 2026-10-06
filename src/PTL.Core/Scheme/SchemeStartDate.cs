namespace PTL.Core.Scheme;

// Port of legacy Scheme.RecalucalateStartDate: the start date is derived from the scheme's
// distribution, never entered by the user (Scheme.aspx renders it read-only and the line that
// would read it back from the form is commented out).
public static class SchemeStartDate
{
    public static DateTime Calculate(Scheme scheme, DateTime contractStartDate)
    {
        if (scheme.DistributionAsAvailable)
        {
            return contractStartDate.AddYears(scheme.YearId - contractStartDate.Year);
        }

        // The first selected month on or after the contract start month falls in the scheme's own
        // year; if none does, the first selected month of the year falls in the following one.
        var months = SelectedMonths(scheme);

        foreach (var month in months.Where(month => contractStartDate.Month <= month))
        {
            return new DateTime(scheme.YearId, month, 1);
        }

        // With nothing selected there is no distribution to derive from, so the scheme starts at
        // the contract start date of its own year - the default legacy stamps on a new scheme.
        return months.Count > 0
            ? new DateTime(scheme.YearId + 1, months[0], 1)
            : contractStartDate.AddYears(scheme.YearId - contractStartDate.Year);
    }

    private static List<int> SelectedMonths(Scheme scheme)
    {
        var flags = new[]
        {
            scheme.DistributionMonthJan, scheme.DistributionMonthFeb, scheme.DistributionMonthMar,
            scheme.DistributionMonthApr, scheme.DistributionMonthMay, scheme.DistributionMonthJun,
            scheme.DistributionMonthJul, scheme.DistributionMonthAug, scheme.DistributionMonthSep,
            scheme.DistributionMonthOct, scheme.DistributionMonthNov, scheme.DistributionMonthDec,
        };

        return [.. Enumerable.Range(1, 12).Where(month => flags[month - 1])];
    }
}
