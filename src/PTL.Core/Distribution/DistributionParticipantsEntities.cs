namespace PTL.Core.Distribution;

// spgMonthlyDistributionScheme result set 2 - legacy DistributionParticipant, full detail (not
// just the SampleNumbers/Schedule screens' Count/NumberOfSetsRequired aggregate).
public class DistributionParticipantEntity
{
    public Guid DistributionParticipantId { get; set; }
    public Guid ParticipantId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public Guid MonthlyDistributionSchemeId { get; set; }

    // Legacy DistributionParticipant.mDeadlineDate (a CSLA SmartDate) - empty/null until the user
    // explicitly runs Change Deadline Date; a brand-new participant (NewParticipant/
    // GetAdditionalParticipant) never sets this.
    public DateTime? DeadlineDate { get; set; }

    // fldMDSDeadlineDate - the owning MonthlyDistributionScheme's own deadline, used as the
    // fallback display value whenever DeadlineDate has no individual override (legacy
    // DistributionParticipant.DeadlineDate: "If Not mDeadlineDate.IsEmpty Then Return
    // mDeadlineDate.Date Else Return mMDSDeadlineDate").
    public DateTime SchemeDeadlineDate { get; set; }

    public DateTime EffectiveDeadlineDate => DeadlineDate ?? SchemeDeadlineDate;

    // Legacy DistributionParticipant.DeadlineChangeReason - a single overwritable column, not a
    // history: only ever holds the reason for the MOST RECENT deadline change.
    public string DeadlineChangeReason { get; set; } = string.Empty;
    public Guid ContractId { get; set; }
    public Guid ParticipantSchemeId { get; set; }
    public bool AdditionalParticipant { get; set; }
    public int NumberOfSetsRequired { get; set; }

    // Legacy DistributionParticipant.FullName: LabCode + ": " + LabName.
    public string FullName => $"{LabCode}: {LabName}";
}

// spgMonthlyDistributionScheme result set 3 - participants eligible to be added to THIS
// distribution via the Contract Item Override function (fnIsParticipantOverrideForMonth), not yet
// enrolled on it. Legacy MonthlyDistributionScheme.AdditionalParticipants.
public class AdditionalParticipantEntity
{
    public Guid ParticipantId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public Guid ParticipantSchemeId { get; set; }
    public Guid ContractId { get; set; }
    public int NumberOfSetsRequired { get; set; }

    public string FullName => $"{LabCode}: {LabName}";
}

public sealed record DistributionParticipantsData(
    MonthlyDistributionSchemeEntity Scheme,
    IReadOnlyList<DistributionParticipantEntity> Participants,
    IReadOnlyList<AdditionalParticipantEntity> AdditionalParticipants);

// Legacy MonthlyDistribution.RepeatDistribution's ParticipantSelectionInfo - which collection
// (Participants vs AdditionalParticipants) a selected id came from.
public sealed record ParticipantSelection(Guid Id, bool IsAdditional);
