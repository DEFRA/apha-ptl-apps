namespace PTL.Core.Distribution;

// The Packaging dropdown's values, legacy PackagingSheet.aspx DropDownPackagingType (0/1/2).
public enum PackagingMode
{
    GroupAddress = 0,
    ParticipantCombined = 1,
    Daily = 2,
}

// spgGroupedPackagingSheet result set 2 (Group Address) / spgPartGroupedPackagingSheet result set 2
// (Participant) / spgWeeklyPackagingSheet result set 2 (Day - only the date) - legacy GroupAddress /
// GroupParticipantInfo / Day. Only the columns that apply to the chosen option are populated.
public sealed class PackagingGroupEntity
{
    public Guid GroupAddressId { get; set; }
    public Guid ParticipantId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public string PackingInfo { get; set; } = string.Empty;
    public DateTime DistributionDate { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

// spgGroupedPackagingSheet / spgWeeklyPackagingSheet result set 3 - legacy PackagingItem.
public sealed class PackagingItemEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public Guid GroupAddressId { get; set; }
    public bool PreparationComplete { get; set; }
    public string SchemeName { get; set; } = string.Empty;
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string DistributionReference { get; set; } = string.Empty;
    public string DistributionReferenceSuffix { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public string PackingInfo { get; set; } = string.Empty;
    public int NumberOfSetsRequired { get; set; }
    public DateTime DistributionDate { get; set; }
    public string CustomsDocumentDescription { get; set; } = string.Empty;
    public string CustomsDocumentVolume { get; set; } = string.Empty;
    public int NumberOfSamples { get; set; }
}

// spgGroupedPackagingSheet / spgWeeklyPackagingSheet result set 4 - legacy ParticipantInfo.
public sealed class PackagingParticipantEntity
{
    public Guid GroupAddressId { get; set; }
    public Guid MonthlyDistributionSchemeId { get; set; }
    public Guid DistributionParticipantId { get; set; }
    public Guid ParticipantId { get; set; }
    public DateTime? PackagedDate { get; set; }
    public DateTime? PostedDate { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public string PackingInfo { get; set; } = string.Empty;
    public string CustomerPackingInfo { get; set; } = string.Empty;
    public string PSLevelPackingInfo { get; set; } = string.Empty;
    public int NumberOfSetsRequired { get; set; }
    public bool ImportExportLicenceRequired { get; set; }
    public bool ImportPermitReceived { get; set; }
    public bool CustomsCertificateRequired { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime DistributionDate { get; set; }
}

// spgPartGroupedPackagingSheet result set 3 - legacy SchemeItem: a participant's scheme with its
// own Packed/Posted dates (one tlnkDistributionParticipant row).
public sealed class PackagingSchemeItemEntity
{
    public Guid ParticipantId { get; set; }
    public Guid DistributionParticipantId { get; set; }
    public Guid MonthlyDistributionSchemeId { get; set; }
    public bool PreparationComplete { get; set; }
    public string DistributionReference { get; set; } = string.Empty;
    public string DistributionReferenceSuffix { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public DateTime? PackagedDate { get; set; }
    public DateTime? PostedDate { get; set; }
    public string PackingInfo { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string CustomsDocumentDescription { get; set; } = string.Empty;
    public string CustomsDocumentVolume { get; set; } = string.Empty;
    public int NumberOfSamples { get; set; }
    public int NumberOfSetsRequired { get; set; }
    public string PSLevelPackingInfo { get; set; } = string.Empty;
    public bool ImportExportLicenceRequired { get; set; }
    public bool ImportPermitReceived { get; set; }
    public bool CustomsCertificateRequired { get; set; }
    public string CustomerPackingInfo { get; set; } = string.Empty;
    public DateTime DistributionDate { get; set; }
}

// Every result set behind one packaging sheet fetch. Groups holds the Group Addresses, Participants
// or Days (by option); Items/Participants are the Group Address/Daily sets; SchemeItems is the
// Participant Combined set.
public sealed record PackagingRawData(
    int YearId,
    int MonthId,
    IReadOnlyList<PackagingGroupEntity> Groups,
    IReadOnlyList<PackagingItemEntity> Items,
    IReadOnlyList<PackagingParticipantEntity> Participants,
    IReadOnlyList<PackagingSchemeItemEntity> SchemeItems);

// spuDistributionParticipantPacking's parameters.
public sealed record PackagingProgressUpdate(Guid DistributionParticipantId, DateTime? PackagedDate, DateTime? PostedDate);

// tblDistributionPostedEmail - spiDistributionPostedEmail / spgaDistributionPostedEmail.
public sealed class DistributionPostedEmailEntity
{
    public Guid DistributionPostedEmailId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid MonthlyDistributionSchemeId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Distribution { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
}

// spgLabelParticipantAddress / spgLabelGroupAddress - only the lines printed on a label.
public sealed class PackagingLabelAddressEntity
{
    public string ContactName { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
}

// Legacy Packed/Posted "total" cell: "Complete" text, an "All" checkbox, or nothing (preparation
// not yet complete).
public enum PackagingTotalState
{
    None,
    Complete,
    Selectable,
}

public sealed record PackagingParticipantRow(
    Guid DistributionParticipantId,
    string Description,
    DateTime? PackagedDate,
    DateTime? PostedDate,
    string ImportPermitStatus);

public sealed record PackagingSchemeRow(
    Guid MonthlyDistributionSchemeId,
    string Description,
    bool PreparationComplete,
    string PackingInfo,
    string CustomerPackingInfo,
    string PSLevelPackingInfo,
    string ImportPermitStatus,
    Guid? DistributionParticipantId,
    DateTime? PackagedDate,
    DateTime? PostedDate,
    PackagingTotalState PackedTotal,
    PackagingTotalState PostedTotal,
    IReadOnlyList<PackagingParticipantRow> Participants);

// One Print-able unit: a (Group Address | Participant | Scheme) on a date, legacy "PrintableRow".
public sealed record PackagingBlock(
    string PrintKey,
    Guid KeyId,
    DateTime Date,
    string Heading,
    string PackingInfo,
    bool DueToBePosted,
    bool ShowAllPacked,
    bool ShowAllPosted,
    IReadOnlyList<PackagingSchemeRow> Schemes);

public sealed record PackagingSheet(
    PackagingMode Mode,
    int Week,
    int YearId,
    int MonthId,
    IReadOnlyList<PackagingBlock> Blocks);

public sealed record PackagingStageUpdate(
    PackagingMode Mode,
    int Week,
    Guid DistributionParticipantId,
    bool Packed,
    bool Posted);

// EmailFailures lists the Distribution Posted emails GOV.UK Notify did not accept; the Save itself still committed.
public sealed record PackagingSaveOutcome(bool Success, IReadOnlyList<string>? EmailFailures = null);

public sealed record PackagingLabel(IReadOnlyList<string> Lines);

public sealed record PackagingCustomsOutcome(bool Success, string? Error, string FileName, byte[] Content);
