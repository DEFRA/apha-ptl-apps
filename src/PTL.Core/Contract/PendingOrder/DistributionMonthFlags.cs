namespace PTL.Core.Contract.PendingOrder;

// Shared by AvailableSchemeEntity, ParticipatingSchemeEntity and PendingOrderSchemeEntity: all
// three map a stored procedure's 12 separate fldJan..fldDec columns to 12 bool properties (Dapper
// needs the real property names for column mapping, so they can't be collapsed into an array).
// This interface lets the month-number <-> property switch be written once instead of per-entity.
public interface IDistributionMonthFlags
{
    bool DistributionMonthJan { get; set; }
    bool DistributionMonthFeb { get; set; }
    bool DistributionMonthMar { get; set; }
    bool DistributionMonthApr { get; set; }
    bool DistributionMonthMay { get; set; }
    bool DistributionMonthJun { get; set; }
    bool DistributionMonthJul { get; set; }
    bool DistributionMonthAug { get; set; }
    bool DistributionMonthSep { get; set; }
    bool DistributionMonthOct { get; set; }
    bool DistributionMonthNov { get; set; }
    bool DistributionMonthDec { get; set; }
}

public static class DistributionMonthFlagsExtensions
{
    public static bool GetMonth(this IDistributionMonthFlags flags, int month) => month switch
    {
        1 => flags.DistributionMonthJan,
        2 => flags.DistributionMonthFeb,
        3 => flags.DistributionMonthMar,
        4 => flags.DistributionMonthApr,
        5 => flags.DistributionMonthMay,
        6 => flags.DistributionMonthJun,
        7 => flags.DistributionMonthJul,
        8 => flags.DistributionMonthAug,
        9 => flags.DistributionMonthSep,
        10 => flags.DistributionMonthOct,
        11 => flags.DistributionMonthNov,
        12 => flags.DistributionMonthDec,
        _ => false
    };

    public static void SetMonth(this IDistributionMonthFlags flags, int month, bool value)
    {
        switch (month)
        {
            case 1: flags.DistributionMonthJan = value; break;
            case 2: flags.DistributionMonthFeb = value; break;
            case 3: flags.DistributionMonthMar = value; break;
            case 4: flags.DistributionMonthApr = value; break;
            case 5: flags.DistributionMonthMay = value; break;
            case 6: flags.DistributionMonthJun = value; break;
            case 7: flags.DistributionMonthJul = value; break;
            case 8: flags.DistributionMonthAug = value; break;
            case 9: flags.DistributionMonthSep = value; break;
            case 10: flags.DistributionMonthOct = value; break;
            case 11: flags.DistributionMonthNov = value; break;
            case 12: flags.DistributionMonthDec = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(month));
        }
    }

    public static bool HasAnyMonth(this IDistributionMonthFlags flags) =>
        Enumerable.Range(1, 12).Any(flags.GetMonth);

    // Legacy GetCurrentlyParticipatingSchemes: the same scheme ordered twice is merged by OR-ing its months.
    public static void MergeMonthFlagsFrom(this IDistributionMonthFlags flags, IDistributionMonthFlags other)
    {
        flags.DistributionMonthJan |= other.DistributionMonthJan;
        flags.DistributionMonthFeb |= other.DistributionMonthFeb;
        flags.DistributionMonthMar |= other.DistributionMonthMar;
        flags.DistributionMonthApr |= other.DistributionMonthApr;
        flags.DistributionMonthMay |= other.DistributionMonthMay;
        flags.DistributionMonthJun |= other.DistributionMonthJun;
        flags.DistributionMonthJul |= other.DistributionMonthJul;
        flags.DistributionMonthAug |= other.DistributionMonthAug;
        flags.DistributionMonthSep |= other.DistributionMonthSep;
        flags.DistributionMonthOct |= other.DistributionMonthOct;
        flags.DistributionMonthNov |= other.DistributionMonthNov;
        flags.DistributionMonthDec |= other.DistributionMonthDec;
    }
}
