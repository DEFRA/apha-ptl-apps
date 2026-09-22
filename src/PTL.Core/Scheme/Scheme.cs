namespace PTL.Core.Scheme;

// Domain entity mapped to tblScheme by PTL.Data's SchemeRepository (Dapper); shape matches the first result set
// returned by spgSchemeBySchemeId (Scheme Core scope only - see docs/analysis/scheme-analysis.md).
// SharedId and LastModified are system-managed; IsReadOnly is computed by the fetch procedure.
public class Scheme
{
    public Guid SchemeId { get; set; }
    public Guid SharedId { get; set; }
    public int YearId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid ScheduleId { get; set; }
    public Guid ScheduleCodeId { get; set; }
    public DateTime? StartDate { get; set; }

    // Legacy screen order is Apr..Sep, AsAvailable, Oct..Mar (financial year), not Jan..Dec -
    // preserved here to match Scheme.aspx field order exactly.
    public bool DistributionMonthApr { get; set; }
    public bool DistributionMonthMay { get; set; }
    public bool DistributionMonthJun { get; set; }
    public bool DistributionMonthJul { get; set; }
    public bool DistributionMonthAug { get; set; }
    public bool DistributionMonthSep { get; set; }
    public bool DistributionAsAvailable { get; set; }
    public bool DistributionMonthOct { get; set; }
    public bool DistributionMonthNov { get; set; }
    public bool DistributionMonthDec { get; set; }
    public bool DistributionMonthJan { get; set; }
    public bool DistributionMonthFeb { get; set; }
    public bool DistributionMonthMar { get; set; }

    public int WeekNumber { get; set; }
    public Guid DayOfWeekId { get; set; }
    public int NumberOfSamples { get; set; }

    // Joined from tblSchedule (fldSampleNoSequence) - display-only, never a spiScheme/spuScheme parameter.
    public int? SampleNoSequence { get; set; }

    public string SampleOrigin { get; set; } = string.Empty;
    public int Deadline { get; set; }
    public string Subcontractor { get; set; } = string.Empty;
    public bool CombinedPackaging { get; set; }
    public Guid? Postage { get; set; }
    public string? CustomsVolume { get; set; }
    public string SamplePackingInstructions { get; set; } = string.Empty;

    // Editable on Create only - locked by SchemeService once the scheme exists (mirrors
    // Scheme.aspx.vb disabling CheckboxRequiresAssessment in edit mode).
    public bool RequiresAssessment { get; set; }

    public bool CommentsRequired { get; set; }
    public bool Pilot { get; set; }
    public bool LimitedSampleAvailability { get; set; }
    public bool Accredited { get; set; }
    public bool NoVLALabs { get; set; }
    public bool ComerciallyAvailable { get; set; }
    public string? CustomsDescription { get; set; }
    public bool DataConsentDeclarationActive { get; set; }
    public string? DataConsentDeclarationText { get; set; }

    public string Instructions { get; set; } = string.Empty;

    // Confirmed bit columns in tblScheme (not dates), despite the field names - preserved as-is.
    public bool DateOfReceipt { get; set; }
    public bool StorageConditions { get; set; }
    public bool ConditionOnReceipt { get; set; }

    public Guid? TestConsultant1 { get; set; }
    public Guid? TestConsultant2 { get; set; }
    public Guid? TestConsultant3 { get; set; }
    public Guid? TestConsultantTabulationId { get; set; }
    public bool UseExternalReference { get; set; }
    public bool StoreRatings { get; set; }
    public Guid? Assessor1 { get; set; }
    public Guid? Assessor2 { get; set; }
    public Guid? Assessor3 { get; set; }
    public Guid? Assessor4 { get; set; }
    public string? StandardTabulationText { get; set; }

    // Stamped by SchemeService on every Create/Update - never trusted from a request.
    public DateTime LastModified { get; set; }

    // Computed by spgSchemeBySchemeId (YearId < current-year), never sent in requests.
    public bool IsReadOnly { get; set; }
}
