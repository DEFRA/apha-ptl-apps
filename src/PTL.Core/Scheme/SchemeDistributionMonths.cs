using PTL.Core.Lookup;

namespace PTL.Core.Scheme;

// Which of a scheme's distribution months may still be changed. True means still editable.
public sealed record SchemeMonthEditability(
    bool Jan, bool Feb, bool Mar, bool Apr, bool May, bool Jun,
    bool Jul, bool Aug, bool Sep, bool Oct, bool Nov, bool Dec)
{
    public static SchemeMonthEditability AllEditable { get; } =
        new(true, true, true, true, true, true, true, true, true, true, true, true);

    public bool CanEdit(int monthNumber) => monthNumber switch
    {
        1 => Jan,
        2 => Feb,
        3 => Mar,
        4 => Apr,
        5 => May,
        6 => Jun,
        7 => Jul,
        8 => Aug,
        9 => Sep,
        10 => Oct,
        11 => Nov,
        12 => Dec,
        _ => true,
    };
}

/// <summary>
/// Port of legacy Scheme.SetEditPermissions / UpdateMonth, which is also what the SQL function
/// dbo.fnIsDistributionNotDefined expresses as fldCanEditJan..Dec on spgSchemeBySchemeId: once a
/// monthly distribution exists for a month of the scheme's financial year, that month's
/// distribution has been initialised and can no longer be turned on or off.
/// </summary>
public static class SchemeDistributionMonths
{
    public static SchemeMonthEditability CalculateEditability(
        int yearId,
        int contractStartMonth,
        IEnumerable<MonthlyDistributionEntity> monthlyDistributions)
    {
        ArgumentNullException.ThrowIfNull(monthlyDistributions);

        // A scheme year runs from the contract start month to the month before it in the next
        // calendar year, so months before the start month belong to yearId + 1.
        var locked = monthlyDistributions
            .Where(distribution =>
                (distribution.YearId == yearId && distribution.MonthId >= contractStartMonth) ||
                (distribution.YearId == yearId + 1 && distribution.MonthId < contractStartMonth))
            .Select(distribution => distribution.MonthId)
            .ToHashSet();

        return new SchemeMonthEditability(
            Jan: !locked.Contains(1),
            Feb: !locked.Contains(2),
            Mar: !locked.Contains(3),
            Apr: !locked.Contains(4),
            May: !locked.Contains(5),
            Jun: !locked.Contains(6),
            Jul: !locked.Contains(7),
            Aug: !locked.Contains(8),
            Sep: !locked.Contains(9),
            Oct: !locked.Contains(10),
            Nov: !locked.Contains(11),
            Dec: !locked.Contains(12));
    }

    public static void ApplyEditability(Scheme scheme, SchemeMonthEditability editability)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(editability);

        scheme.CanEditJan = editability.Jan;
        scheme.CanEditFeb = editability.Feb;
        scheme.CanEditMar = editability.Mar;
        scheme.CanEditApr = editability.Apr;
        scheme.CanEditMay = editability.May;
        scheme.CanEditJun = editability.Jun;
        scheme.CanEditJul = editability.Jul;
        scheme.CanEditAug = editability.Aug;
        scheme.CanEditSep = editability.Sep;
        scheme.CanEditOct = editability.Oct;
        scheme.CanEditNov = editability.Nov;
        scheme.CanEditDec = editability.Dec;
    }

    /// <summary>
    /// Mirrors the legacy DistributionMonthX property setters, which ignore an assignment when
    /// that month is locked ("If mDistributionMonthX &lt;&gt; value And mCanEditX Then") - a locked
    /// month keeps the value it already had, whatever was posted.
    /// </summary>
    public static void RestoreLockedMonths(Scheme scheme, SchemeMonthEditability editability, Scheme? existing)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(editability);

        if (!editability.Jan)
        {
            scheme.DistributionMonthJan = existing?.DistributionMonthJan ?? false;
        }

        if (!editability.Feb)
        {
            scheme.DistributionMonthFeb = existing?.DistributionMonthFeb ?? false;
        }

        if (!editability.Mar)
        {
            scheme.DistributionMonthMar = existing?.DistributionMonthMar ?? false;
        }

        if (!editability.Apr)
        {
            scheme.DistributionMonthApr = existing?.DistributionMonthApr ?? false;
        }

        if (!editability.May)
        {
            scheme.DistributionMonthMay = existing?.DistributionMonthMay ?? false;
        }

        if (!editability.Jun)
        {
            scheme.DistributionMonthJun = existing?.DistributionMonthJun ?? false;
        }

        if (!editability.Jul)
        {
            scheme.DistributionMonthJul = existing?.DistributionMonthJul ?? false;
        }

        if (!editability.Aug)
        {
            scheme.DistributionMonthAug = existing?.DistributionMonthAug ?? false;
        }

        if (!editability.Sep)
        {
            scheme.DistributionMonthSep = existing?.DistributionMonthSep ?? false;
        }

        if (!editability.Oct)
        {
            scheme.DistributionMonthOct = existing?.DistributionMonthOct ?? false;
        }

        if (!editability.Nov)
        {
            scheme.DistributionMonthNov = existing?.DistributionMonthNov ?? false;
        }

        if (!editability.Dec)
        {
            scheme.DistributionMonthDec = existing?.DistributionMonthDec ?? false;
        }
    }
}
