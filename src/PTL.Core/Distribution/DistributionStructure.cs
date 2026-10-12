namespace PTL.Core.Distribution;

// The in-memory distribution structure legacy IntendedResultsSet.DataPortal_Create builds from
// the Scheme when Sample Numbers are opened for the first time. Every id is freshly generated:
// the distribution gets its OWN copy of the scheme's tests, items and tabulations so that later
// edits to the Scheme cannot retrospectively change a distribution that has already gone out.
public sealed record DistributionStructure(
    IReadOnlyList<NewDistributionTest> Tests,
    IReadOnlyList<NewDistributionTabulation> Tabulations,
    IReadOnlyList<NewDistributionSample> Samples,
    Guid TestConsultantTabulationId,
    DateTime SchemeVersionDate,
    bool DateOfReceipt,
    bool StorageConditions,
    bool ConditionOnReceipt,
    string Instructions,
    string StandardTabulationText);

public sealed record NewDistributionTest(
    Guid TestId,
    Guid TestTypeId,
    int Order,
    IReadOnlyList<NewDistributionTestResultItem> ResultItems,
    IReadOnlyList<NewDistributionTestMethodItem> MethodItems,
    IReadOnlyList<NewDistributionCategoryItem> CategoryItems);

// IsRange is always false at creation - legacy TestResultItem.NewTestResultItem never sets it;
// it becomes meaningful later on the Intended Results screen.
public sealed record NewDistributionTestResultItem(
    Guid TestResultItemId,
    Guid TestId,
    Guid TestResultItemTypeId,
    int Order)
{
    public bool IsRange { get; init; }
}

// IsApplicable is false at creation (legacy sets mIsApplicable = False) and IntendedResult is
// empty until the Intended Results screen is used.
public sealed record NewDistributionTestMethodItem(
    Guid TestMethodItemId,
    Guid TestId,
    Guid TestMethodItemTypeId,
    int Order)
{
    public string IntendedResult { get; init; } = string.Empty;

    public bool IsApplicable { get; init; }
}

public sealed record NewDistributionCategoryItem(
    Guid CategoryItemId,
    Guid TestId,
    Guid CategoryItemTypeId,
    int Order,
    IReadOnlyList<NewDistributionCriterionItem> CriterionItems);

public sealed record NewDistributionCriterionItem(
    Guid CriterionItemId,
    Guid CategoryItemId,
    Guid CriterionItemTypeId,
    int Order);

public sealed record NewDistributionTabulation(
    Guid TabulationId,
    Guid MonthlyDistributionSchemeId,
    string Name,
    bool IntendedResultsOnly,
    DateTime SchemeVersionDate,
    bool SingleParticipantTabulation,
    bool ShowRatings,
    bool AvailableToParticipants,
    bool AvailableToViewers,
    IReadOnlyList<NewDistributionTabulationItem> ResultItems,
    IReadOnlyList<NewDistributionTabulationItem> MethodItems);

public sealed record NewDistributionTabulationItem(Guid LinkId, Guid TabulationId, Guid ItemId);

// SampleNumber is assigned by the repository, inside the creation transaction, because
// spgNextSampleNumber advances a counter on tblSchedule and must roll back with everything else.
public sealed class NewDistributionSample
{
    public Guid SampleId { get; init; }

    public string SampleNumber { get; set; } = string.Empty;

    public IReadOnlyList<NewDistributionSampleTest> SampleTests { get; init; } = [];
}

public sealed record NewDistributionSampleTest(
    Guid SampleTestId,
    Guid SampleId,
    Guid TestId,
    bool IsApplicable,
    IReadOnlyList<NewDistributionSampleResultItem> ResultItems);

public sealed record NewDistributionSampleResultItem(
    Guid SampleResultItemId,
    Guid SampleTestId,
    Guid TestResultItemId);

// Raised when the Scheme cannot produce a valid distribution structure. Mirrors
// SchemeValidationException: PTL.Api turns it into a 400 ValidationProblem rather than letting a
// partially configured distribution be created.
public sealed class DistributionStructureException(string message) : Exception(message);
