using PTL.Core.Contract.PendingOrder;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Core.Contract.CreateOrder;

// Row shape of spgaAvailableSchemeInfoByYearId (legacy AvailableSchemeCollection + the
// SchemeFullInfoList the page uses for month/postage metadata): every scheme of the year. CanEdit*
// is dbo.fnIsDistributionNotPosted - false once that month's distribution has been posted.
public class AvailableSchemeEntity : IDistributionMonthFlags
{
    public Guid SchemeId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public bool ComerciallyAvailable { get; set; }
    public Guid? Postage { get; set; }
    public bool CombinedPackaging { get; set; }
    public Guid DayOfWeekId { get; set; }
    public int WeekNumber { get; set; }

    public bool DistributionMonthJan { get; set; }
    public bool DistributionMonthFeb { get; set; }
    public bool DistributionMonthMar { get; set; }
    public bool DistributionMonthApr { get; set; }
    public bool DistributionMonthMay { get; set; }
    public bool DistributionMonthJun { get; set; }
    public bool DistributionMonthJul { get; set; }
    public bool DistributionMonthAug { get; set; }
    public bool DistributionMonthSep { get; set; }
    public bool DistributionMonthOct { get; set; }
    public bool DistributionMonthNov { get; set; }
    public bool DistributionMonthDec { get; set; }

    public bool CanEditJan { get; set; } = true;
    public bool CanEditFeb { get; set; } = true;
    public bool CanEditMar { get; set; } = true;
    public bool CanEditApr { get; set; } = true;
    public bool CanEditMay { get; set; } = true;
    public bool CanEditJun { get; set; } = true;
    public bool CanEditJul { get; set; } = true;
    public bool CanEditAug { get; set; } = true;
    public bool CanEditSep { get; set; } = true;
    public bool CanEditOct { get; set; } = true;
    public bool CanEditNov { get; set; } = true;
    public bool CanEditDec { get; set; } = true;

    public bool Offers(int month) => this.GetMonth(month);

    public bool CanEdit(int month) => month switch
    {
        1 => CanEditJan,
        2 => CanEditFeb,
        3 => CanEditMar,
        4 => CanEditApr,
        5 => CanEditMay,
        6 => CanEditJun,
        7 => CanEditJul,
        8 => CanEditAug,
        9 => CanEditSep,
        10 => CanEditOct,
        11 => CanEditNov,
        12 => CanEditDec,
        _ => false
    };

    // The subset of Scheme that PendingOrderPricing reads.
    public CoreScheme ToPricingScheme() => new()
    {
        SchemeId = SchemeId,
        Postage = Postage,
        CombinedPackaging = CombinedPackaging,
        DayOfWeekId = DayOfWeekId,
        WeekNumber = WeekNumber
    };
}

// Row shape of spgPastParticipantSchemeByParticipantSchemeId: the months the participant already
// holds on a scheme (matched to the page year's scheme by identifier).
public class ParticipatingSchemeEntity : IDistributionMonthFlags
{
    public Guid ParticipantId { get; set; }
    public Guid SchemeId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;

    public bool DistributionMonthJan { get; set; }
    public bool DistributionMonthFeb { get; set; }
    public bool DistributionMonthMar { get; set; }
    public bool DistributionMonthApr { get; set; }
    public bool DistributionMonthMay { get; set; }
    public bool DistributionMonthJun { get; set; }
    public bool DistributionMonthJul { get; set; }
    public bool DistributionMonthAug { get; set; }
    public bool DistributionMonthSep { get; set; }
    public bool DistributionMonthOct { get; set; }
    public bool DistributionMonthNov { get; set; }
    public bool DistributionMonthDec { get; set; }

    public bool Holds(int month) => this.GetMonth(month);

    // Legacy GetCurrentlyParticipatingSchemes: the same scheme ordered twice is merged by OR-ing its months.
    public void MergeMonthsFrom(ParticipatingSchemeEntity other) => ((IDistributionMonthFlags)this).MergeMonthFlagsFrom(other);
}
