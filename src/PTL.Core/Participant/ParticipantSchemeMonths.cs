namespace PTL.Core.Participant;

// Jan..Dec views over ParticipantSchemeRecord's per-month properties, so the month rules are written
// once instead of twelve times.
public static class ParticipantSchemeMonths
{
    public const int Count = 12;

    public static readonly IReadOnlyList<string> DistributionFields =
    [
        "DistributionMonthJan", "DistributionMonthFeb", "DistributionMonthMar", "DistributionMonthApr",
        "DistributionMonthMay", "DistributionMonthJun", "DistributionMonthJul", "DistributionMonthAug",
        "DistributionMonthSep", "DistributionMonthOct", "DistributionMonthNov", "DistributionMonthDec"
    ];

    public static bool[] Distribution(ParticipantSchemeRecord r) =>
    [
        r.DistributionMonthJan, r.DistributionMonthFeb, r.DistributionMonthMar, r.DistributionMonthApr,
        r.DistributionMonthMay, r.DistributionMonthJun, r.DistributionMonthJul, r.DistributionMonthAug,
        r.DistributionMonthSep, r.DistributionMonthOct, r.DistributionMonthNov, r.DistributionMonthDec
    ];

    public static bool[] CanEdit(ParticipantSchemeRecord r) =>
    [
        r.CanEditJan, r.CanEditFeb, r.CanEditMar, r.CanEditApr, r.CanEditMay, r.CanEditJun,
        r.CanEditJul, r.CanEditAug, r.CanEditSep, r.CanEditOct, r.CanEditNov, r.CanEditDec
    ];

    public static bool[] Override(ParticipantSchemeRecord r) =>
    [
        r.IsOverrideJan, r.IsOverrideFeb, r.IsOverrideMar, r.IsOverrideApr, r.IsOverrideMay, r.IsOverrideJun,
        r.IsOverrideJul, r.IsOverrideAug, r.IsOverrideSep, r.IsOverrideOct, r.IsOverrideNov, r.IsOverrideDec
    ];

    public static void SetDistribution(ParticipantSchemeRecord r, IReadOnlyList<bool> v)
    {
        r.DistributionMonthJan = v[0];
        r.DistributionMonthFeb = v[1];
        r.DistributionMonthMar = v[2];
        r.DistributionMonthApr = v[3];
        r.DistributionMonthMay = v[4];
        r.DistributionMonthJun = v[5];
        r.DistributionMonthJul = v[6];
        r.DistributionMonthAug = v[7];
        r.DistributionMonthSep = v[8];
        r.DistributionMonthOct = v[9];
        r.DistributionMonthNov = v[10];
        r.DistributionMonthDec = v[11];
    }

    public static void SetOverride(ParticipantSchemeRecord r, IReadOnlyList<bool> v)
    {
        r.IsOverrideJan = v[0];
        r.IsOverrideFeb = v[1];
        r.IsOverrideMar = v[2];
        r.IsOverrideApr = v[3];
        r.IsOverrideMay = v[4];
        r.IsOverrideJun = v[5];
        r.IsOverrideJul = v[6];
        r.IsOverrideAug = v[7];
        r.IsOverrideSep = v[8];
        r.IsOverrideOct = v[9];
        r.IsOverrideNov = v[10];
        r.IsOverrideDec = v[11];
    }

    public static void SetCanEdit(ParticipantSchemeRecord r, IReadOnlyList<bool> v)
    {
        r.CanEditJan = v[0];
        r.CanEditFeb = v[1];
        r.CanEditMar = v[2];
        r.CanEditApr = v[3];
        r.CanEditMay = v[4];
        r.CanEditJun = v[5];
        r.CanEditJul = v[6];
        r.CanEditAug = v[7];
        r.CanEditSep = v[8];
        r.CanEditOct = v[9];
        r.CanEditNov = v[10];
        r.CanEditDec = v[11];
    }

    // Legacy ParticipantScheme.DistributionMonthX setter: `If mDistributionMonthX <> value And
    // (mCanEditX Or mIsOverrideX)` - a locked month (distribution already posted) keeps its stored
    // value. A month ticked in Override mode becomes an override only if it was locked and not already
    // overridden (the only months legacy enables in Override mode), so an ordinary editable month is
    // never flagged and a stored override is never cleared. DELIBERATE DEVIATION: legacy also lets a
    // stored override month follow the (disabled, hence unticked) checkbox, silently wiping it on the
    // next ordinary save - that data loss is not reproduced. Returns the effective months/overrides.
    public static (bool[] Months, bool[] Overrides) ApplyLocks(
        IReadOnlyList<bool> storedMonths,
        IReadOnlyList<bool> storedOverrides,
        IReadOnlyList<bool> canEdit,
        IReadOnlyList<bool> requestedMonths,
        IReadOnlyList<bool> requestedOverrides)
    {
        var months = new bool[Count];
        var overrides = new bool[Count];
        for (var i = 0; i < Count; i++)
        {
            var newOverride = requestedOverrides[i] && requestedMonths[i] && !canEdit[i] && !storedOverrides[i];
            overrides[i] = storedOverrides[i] || newOverride;
            months[i] = canEdit[i] || newOverride ? requestedMonths[i] : storedMonths[i];
        }

        return (months, overrides);
    }
}
