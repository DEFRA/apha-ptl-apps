namespace PTL.Core.Distribution;

// spgIntendedResults result set 1 - the distribution-level row behind legacy
// IntendedResultsSet.Fetch. Comments/HasIntendedResults/HasSampleNumbersDefined/
// TestConsultantTabulationId are the four columns spuIntendedResults writes back, so every one of
// them must be round-tripped on save even though the Sample Numbers screen only edits Comments.
public class IntendedResultsHeaderEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public string? Comments { get; set; }
    public bool HasIntendedResults { get; set; }
    public bool HasSampleNumbersDefined { get; set; }
    public Guid TestConsultantTabulationId { get; set; }
    public bool StoreRatings { get; set; }
    public bool RequiresAssessment { get; set; }
    public DateTime? IsReadyForAssessor { get; set; }
}

// spgIntendedResults result set 2 (tblSample), ordered by fldSampleNumber.
public class DistributionSampleEntity
{
    public Guid SampleId { get; set; }
    public Guid DistributionSchemeId { get; set; }
    public string? SampleNumber { get; set; }
    public string? InternalDescription { get; set; }
    public string? ExternalDescription { get; set; }
    public string? TabulationDescription { get; set; }
    public string? TestConsultantsDescription { get; set; }
}

// spgIntendedResults result set 3 (tblSampleTest) - one row per sample/test pair; fldIsApplicable
// is the Sample Allocation tick box.
public class DistributionSampleTestEntity
{
    public Guid SampleTestId { get; set; }
    public Guid SampleId { get; set; }
    public Guid TestId { get; set; }
    public bool IsApplicable { get; set; }
}

// spgIntendedResults result set 5 (tblDistributionTest), ordered by fldOrder - one tab per test.
public class DistributionTestEntity
{
    public Guid TestId { get; set; }
    public Guid TestTypeId { get; set; }
    public string? TestName { get; set; }
    public int Order { get; set; }
}

// spgIntendedResults result set 6 (tblDistributionTestMethodItem), ordered by fldOrder.
public class DistributionTestMethodItemEntity
{
    public Guid TestMethodItemId { get; set; }
    public Guid TestId { get; set; }
    public string? Name { get; set; }
    public int Order { get; set; }
}

// spgIntendedResults result set 7 (tblDistributionTestResultItem), ordered by fldOrder.
public class DistributionTestResultItemEntity
{
    public Guid TestResultItemId { get; set; }
    public Guid TestId { get; set; }
    public string? Name { get; set; }
    public int Order { get; set; }
}

// spgIntendedResults result set 8 (tblDistributionCategoryItem) - only rendered when the scheme
// requires assessment.
public class DistributionCategoryItemEntity
{
    public Guid CategoryItemId { get; set; }
    public Guid TestId { get; set; }
    public string? Name { get; set; }
    public int Order { get; set; }
}

// spgIntendedResults result set 9 (tblDistributionCriterionItem), ordered by category then
// criterion order - legacy renders these as the bracketed CriteriaText beside each Category Item.
public class DistributionCriterionItemEntity
{
    public Guid CriterionItemId { get; set; }
    public Guid CategoryItemId { get; set; }
    public Guid TestId { get; set; }
    public string? Name { get; set; }
    public int Order { get; set; }
}

// Everything spgIntendedResults returns, before it is shaped into the Sample Numbers screen model.
public sealed record IntendedResultsData(
    IntendedResultsHeaderEntity Header,
    IReadOnlyList<DistributionSampleEntity> Samples,
    IReadOnlyList<DistributionSampleTestEntity> SampleTests,
    IReadOnlyList<DistributionTestEntity> Tests,
    IReadOnlyList<DistributionTestMethodItemEntity> TestMethodItems,
    IReadOnlyList<DistributionTestResultItemEntity> TestResultItems,
    IReadOnlyList<DistributionCategoryItemEntity> CategoryItems,
    IReadOnlyList<DistributionCriterionItemEntity> CriterionItems);
