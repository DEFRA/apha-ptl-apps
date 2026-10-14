namespace PTL.Core.Distribution;

// Intended Results (legacy Scheduling/IntendedResults.aspx) read/save behaviour.
public interface IIntendedResultsService
{
    Task<IntendedResultsPage?> GetIntendedResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<IntendedResultsSaveOutcome> SaveIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, IntendedResultsUpdate update, CancellationToken cancellationToken = default);
}

public sealed class IntendedResultsService(IDistributionRepository distributionRepository) : IIntendedResultsService
{
    // Legacy IntendedResultsSet.AuditObjectName(), shown as the heading on Audit.aspx.
    private const string AuditObjectNameValue = "Intended Results Set";

    public async Task<IntendedResultsPage?> GetIntendedResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var scheme = await distributionRepository.GetMonthlyDistributionSchemeAsync(monthlyDistributionSchemeId, cancellationToken);

        // Legacy Page_Load: "Samples numbers must be defined before setting the Intended
        // Results" - If Not mds.HasSampleNumbersDefined Then LeavePage(). No error is shown; the
        // user is simply returned to Scheduling, which the controller replicates.
        if (scheme is null || !scheme.HasSampleNumbersDefined)
        {
            return null;
        }

        var data = await distributionRepository.GetIntendedResultsAsync(monthlyDistributionSchemeId, cancellationToken);
        return data is null ? null : BuildPage(scheme, data);
    }

    // Legacy ButtonSave_Click:
    //   mIntendedResults.HasIntendedResults = True                      (unconditional)
    //   If mIntendedResults.RequireAudit Then <Audit.aspx>               (checked BEFORE IsDirty -
    //                                                                      diverts even when nothing
    //                                                                      in THIS save is dirty)
    //   ElseIf mIntendedResults.IsDirty Then Save() : LeavePage()
    //   Else LeavePage()
    // RequireAudit is update.RequiresAuditReason here - computed once at GET time from the
    // pre-edit stored values, and round-tripped unchanged for the whole edit session exactly like
    // legacy's Session-held IntendedResultsSet, never recomputed from what the user posts.
    public async Task<IntendedResultsSaveOutcome> SaveIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, IntendedResultsUpdate update, CancellationToken cancellationToken = default)
    {
        var data = await distributionRepository.GetIntendedResultsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (data is null)
        {
            return IntendedResultsSaveOutcome.Failed(new Dictionary<Guid, IReadOnlyList<SampleNumbersError>>());
        }

        var fieldErrors = CollectFieldErrors(update, data);
        if (fieldErrors.Count > 0)
        {
            return IntendedResultsSaveOutcome.Failed(fieldErrors);
        }

        var changes = CollectChanges(update, data);

        var header = data.Header;
        var headerChanged = !header.HasIntendedResults;
        header.HasIntendedResults = true;

        // RequireAudit is checked BEFORE the dirty/no-op check, exactly like legacy's unconditional
        // "If RequireAudit Then <Audit.aspx>" - a gated distribution always stops for a reason
        // first, even on a save where this particular submission has nothing dirty.
        if (update.RequiresAuditReason && string.IsNullOrWhiteSpace(update.AuditWhy))
        {
            return IntendedResultsSaveOutcome.NeedsAuditReason(AuditObjectNameValue);
        }

        if (!update.RequiresAuditReason && !headerChanged && changes.IsEmpty)
        {
            return IntendedResultsSaveOutcome.Succeeded();
        }

        // Audit rows are only ever written when the Audit.aspx-equivalent step actually ran
        // (legacy's mAudited guard) - a save that bypassed the gate (RequiresAuditReason was
        // false) writes no audit trail at all, matching "first entry doesn't need auditing".
        var auditedTestMethodItemIds = update.RequiresAuditReason ? changes.AuditedTestMethodItemIds : [];
        var auditedSampleResultItems = update.RequiresAuditReason ? changes.AuditedSampleResultItems : [];

        await distributionRepository.UpdateIntendedResultsAsync(
            new IntendedResultsWrite(
                header,
                changes.TestMethodItems,
                auditedTestMethodItemIds,
                changes.TestResultItems,
                changes.SampleResultItems,
                auditedSampleResultItems,
                update.AuditWho,
                update.AuditWhy),
            cancellationToken);

        return IntendedResultsSaveOutcome.Succeeded();
    }

    // Legacy IntendedResultsSet.CheckRequireAudit(): true the moment ANY intended result anywhere
    // in the distribution was already non-empty when fetched - i.e. this is not the first time
    // results are being entered, so every future save on this screen must collect a reason first.
    private static bool ComputeRequiresAuditReason(IntendedResultsData data) =>
        data.TestMethodItems.Any(m => !string.IsNullOrEmpty(m.IntendedResult)) ||
        data.SampleResultItems.Any(s => !string.IsNullOrEmpty(s.IntendedResult) || !string.IsNullOrEmpty(s.IntendedResult2));

    private static IntendedResultsPage BuildPage(MonthlyDistributionSchemeEntity scheme, IntendedResultsData data)
    {
        var sampleTestLookup = data.SampleTests.ToDictionary(st => (st.SampleId, st.TestId));
        var sampleResultItemLookup = data.SampleResultItems.ToDictionary(sri => (sri.SampleTestId, sri.TestResultItemId));

        var tests = data.Tests
            .OrderBy(t => t.Order)
            .Select(t => BuildTest(t, data, sampleTestLookup, sampleResultItemLookup))
            .ToList();

        return new IntendedResultsPage(
            scheme.MonthlyDistributionSchemeId,
            scheme.DistributionReferenceFull,
            scheme.SchemeIdentifier,
            scheme.SchemeName,
            ComputeRequiresAuditReason(data),
            tests,
            data.Header.SchemeVersionDate,
            data.Header.HasIntendedResults,
            data.Header.DateOfReceipt,
            data.Header.StorageConditions,
            data.Header.ConditionOnReceipt,
            data.Header.Instructions ?? string.Empty);
    }

    private static IntendedResultsTest BuildTest(
        DistributionTestEntity test,
        IntendedResultsData data,
        Dictionary<(Guid SampleId, Guid TestId), DistributionSampleTestEntity> sampleTestLookup,
        Dictionary<(Guid SampleTestId, Guid TestResultItemId), DistributionSampleResultItemEntity> sampleResultItemLookup)
    {
        var resultItems = data.TestResultItems
            .Where(r => r.TestId == test.TestId)
            .OrderBy(r => r.Order)
            .Select(r => new IntendedResultsResultItem(r.TestResultItemId, r.Name ?? string.Empty, r.FormatString ?? string.Empty, r.ReturnValueType ?? string.Empty, r.IsRange, r.ExpectedLength))
            .ToList();

        var methodItems = data.TestMethodItems
            .Where(m => m.TestId == test.TestId)
            .OrderBy(m => m.Order)
            .Select(m => new IntendedResultsMethodItem(m.TestMethodItemId, m.Name ?? string.Empty, m.FormatString ?? string.Empty, m.ReturnValueType ?? string.Empty, m.IntendedResult ?? string.Empty))
            .ToList();

        var samples = data.Samples
            .Select(s => BuildSampleRow(s, test.TestId, resultItems, sampleTestLookup, sampleResultItemLookup))
            .ToList();

        return new IntendedResultsTest(test.TestId, test.TestName ?? string.Empty, resultItems, methodItems, samples);
    }

    private static IntendedResultsSampleRow BuildSampleRow(
        DistributionSampleEntity sample,
        Guid testId,
        IReadOnlyList<IntendedResultsResultItem> resultItems,
        Dictionary<(Guid SampleId, Guid TestId), DistributionSampleTestEntity> sampleTestLookup,
        Dictionary<(Guid SampleTestId, Guid TestResultItemId), DistributionSampleResultItemEntity> sampleResultItemLookup)
    {
        var hasSampleTest = sampleTestLookup.TryGetValue((sample.SampleId, testId), out var sampleTest);

        var values = resultItems.Select(resultItem =>
        {
            DistributionSampleResultItemEntity? entity = null;
            if (hasSampleTest)
            {
                sampleResultItemLookup.TryGetValue((sampleTest!.SampleTestId, resultItem.TestResultItemId), out entity);
            }

            return new IntendedResultsSampleResultValue(
                entity?.SampleResultItemId ?? Guid.Empty,
                resultItem.TestResultItemId,
                entity?.IntendedResult ?? string.Empty,
                entity?.IntendedResult2 ?? string.Empty);
        }).ToList();

        return new IntendedResultsSampleRow(
            sample.SampleId,
            sample.SampleNumber ?? string.Empty,
            sample.InternalDescription ?? string.Empty,
            hasSampleTest && sampleTest!.IsApplicable,
            values,
            sample.ExternalDescription ?? string.Empty);
    }

    private static Dictionary<Guid, IReadOnlyList<SampleNumbersError>> CollectFieldErrors(IntendedResultsUpdate update, IntendedResultsData data)
    {
        var resultItemsById = data.TestResultItems.ToDictionary(r => r.TestResultItemId);
        var methodItemsById = data.TestMethodItems.ToDictionary(m => m.TestMethodItemId);
        var sampleResultItemsById = data.SampleResultItems.ToDictionary(s => s.SampleResultItemId);

        var errors = new Dictionary<Guid, IReadOnlyList<SampleNumbersError>>();

        foreach (var test in update.Tests)
        {
            CollectMethodItemErrors(test, methodItemsById, errors);
            CollectSampleErrors(test, sampleResultItemsById, resultItemsById, errors);
        }

        return errors;
    }

    private static void CollectMethodItemErrors(
        IntendedResultsTestUpdate test,
        Dictionary<Guid, DistributionTestMethodItemEntity> methodItemsById,
        Dictionary<Guid, IReadOnlyList<SampleNumbersError>> errors)
    {
        foreach (var methodItem in test.MethodItems)
        {
            if (!methodItemsById.TryGetValue(methodItem.TestMethodItemId, out var stored))
            {
                continue;
            }

            var message = IntendedResultsValidator.ValidateValue(stored.FormatString, stored.ReturnValueType, methodItem.IntendedResult);
            if (message is not null)
            {
                errors[methodItem.TestMethodItemId] = [new SampleNumbersError("IntendedResult", message)];
            }
        }
    }

    private static void CollectSampleErrors(
        IntendedResultsTestUpdate test,
        Dictionary<Guid, DistributionSampleResultItemEntity> sampleResultItemsById,
        Dictionary<Guid, DistributionTestResultItemEntity> resultItemsById,
        Dictionary<Guid, IReadOnlyList<SampleNumbersError>> errors)
    {
        foreach (var sample in test.Samples)
        {
            // Legacy disables the Result Item controls entirely for a non-applicable
            // sample/test pair, and a disabled RegularExpressionValidator never fires.
            if (!sample.IsApplicable)
            {
                continue;
            }

            foreach (var value in sample.Values)
            {
                CollectValueErrors(value, sampleResultItemsById, resultItemsById, errors);
            }
        }
    }

    private static void CollectValueErrors(
        IntendedResultsSampleResultValueUpdate value,
        Dictionary<Guid, DistributionSampleResultItemEntity> sampleResultItemsById,
        Dictionary<Guid, DistributionTestResultItemEntity> resultItemsById,
        Dictionary<Guid, IReadOnlyList<SampleNumbersError>> errors)
    {
        if (!sampleResultItemsById.TryGetValue(value.SampleResultItemId, out var stored) ||
            !resultItemsById.TryGetValue(stored.TestResultItemId, out var resultItem))
        {
            return;
        }

        var itemErrors = new List<SampleNumbersError>();
        var message = IntendedResultsValidator.ValidateValue(resultItem.FormatString, resultItem.ReturnValueType, value.IntendedResult);
        if (message is not null)
        {
            itemErrors.Add(new SampleNumbersError("IntendedResult", message));
        }

        if (resultItem.IsRange)
        {
            var message2 = IntendedResultsValidator.ValidateValue(resultItem.FormatString, resultItem.ReturnValueType, value.IntendedResult2);
            if (message2 is not null)
            {
                itemErrors.Add(new SampleNumbersError("IntendedResult2", message2));
            }
        }

        if (itemErrors.Count > 0)
        {
            errors[value.SampleResultItemId] = itemErrors;
        }
    }

    private sealed record IntendedResultsChanges(
        List<DistributionTestMethodItemEntity> TestMethodItems,
        List<Guid> AuditedTestMethodItemIds,
        List<DistributionTestResultItemEntity> TestResultItems,
        List<DistributionSampleResultItemEntity> SampleResultItems,
        List<AuditedSampleResultItemChange> AuditedSampleResultItems)
    {
        public bool IsEmpty => TestMethodItems.Count == 0 && TestResultItems.Count == 0 && SampleResultItems.Count == 0;
    }

    private static IntendedResultsChanges CollectChanges(IntendedResultsUpdate update, IntendedResultsData data)
    {
        var methodItemsById = data.TestMethodItems.ToDictionary(m => m.TestMethodItemId);
        var resultItemsById = data.TestResultItems.ToDictionary(r => r.TestResultItemId);
        var sampleResultItemsById = data.SampleResultItems.ToDictionary(s => s.SampleResultItemId);

        var changes = new IntendedResultsChanges([], [], [], [], []);

        foreach (var test in update.Tests)
        {
            CollectMethodItemChanges(test.MethodItems, methodItemsById, changes);
            CollectResultItemChanges(test.ResultItems, resultItemsById, changes);

            var postedIsRangeByResultItemId = test.ResultItems.ToDictionary(r => r.TestResultItemId, r => r.IsRange);
            foreach (var sample in test.Samples)
            {
                // Mirrors legacy's disabled controls: a non-applicable sample/test pair is never
                // posted back by a real browser, so it must never be read as "changed to blank".
                if (!sample.IsApplicable)
                {
                    continue;
                }

                CollectSampleResultItemChanges(sample.Values, sampleResultItemsById, postedIsRangeByResultItemId, changes);
            }
        }

        return changes;
    }

    private static void CollectMethodItemChanges(
        IReadOnlyList<IntendedResultsMethodItemUpdate> posted,
        Dictionary<Guid, DistributionTestMethodItemEntity> stored,
        IntendedResultsChanges changes)
    {
        foreach (var item in posted)
        {
            if (!stored.TryGetValue(item.TestMethodItemId, out var entity) || Matches(entity.IntendedResult, item.IntendedResult))
            {
                continue;
            }

            entity.IntendedResult = item.IntendedResult;
            changes.TestMethodItems.Add(entity);
            changes.AuditedTestMethodItemIds.Add(entity.TestMethodItemId);
        }
    }

    private static void CollectResultItemChanges(
        IReadOnlyList<IntendedResultsResultItemUpdate> posted,
        Dictionary<Guid, DistributionTestResultItemEntity> stored,
        IntendedResultsChanges changes)
    {
        foreach (var item in posted)
        {
            if (!stored.TryGetValue(item.TestResultItemId, out var entity) || entity.IsRange == item.IsRange)
            {
                continue;
            }

            entity.IsRange = item.IsRange;
            changes.TestResultItems.Add(entity);
        }
    }

    // IntendedResult2 is only ever considered when the posted IsRange is true for this result
    // item - legacy's second control is never even rendered (so never postable) outside Range
    // mode, and unticking Range does NOT clear a previously-saved IntendedResult2 (the orphaned
    // control simply stops existing; its value is left exactly as it was).
    private static void CollectSampleResultItemChanges(
        IReadOnlyList<IntendedResultsSampleResultValueUpdate> posted,
        Dictionary<Guid, DistributionSampleResultItemEntity> stored,
        Dictionary<Guid, bool> postedIsRangeByResultItemId,
        IntendedResultsChanges changes)
    {
        foreach (var value in posted)
        {
            if (!stored.TryGetValue(value.SampleResultItemId, out var entity))
            {
                continue;
            }

            var isRange = postedIsRangeByResultItemId.GetValueOrDefault(entity.TestResultItemId);
            var resultChanged = !Matches(entity.IntendedResult, value.IntendedResult);
            var result2Changed = isRange && !Matches(entity.IntendedResult2, value.IntendedResult2);
            if (!resultChanged && !result2Changed)
            {
                continue;
            }

            entity.IntendedResult = value.IntendedResult;
            if (isRange)
            {
                entity.IntendedResult2 = value.IntendedResult2;
            }

            changes.SampleResultItems.Add(entity);
            changes.AuditedSampleResultItems.Add(new AuditedSampleResultItemChange(entity.SampleResultItemId, resultChanged, result2Changed));
        }
    }

    // tblDistributionTestMethodItem/tblSampleResultItem columns are nullable, so a stored NULL and
    // a posted "" are the same value.
    private static bool Matches(string? stored, string? posted) =>
        string.Equals(stored ?? string.Empty, posted ?? string.Empty, StringComparison.Ordinal);
}
