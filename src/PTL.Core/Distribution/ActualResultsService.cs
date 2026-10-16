namespace PTL.Core.Distribution;

// Actual Results (legacy Internal User/ActualResults.aspx + ActualResultsSet) read/save behaviour.
public interface IActualResultsService
{
    Task<ActualResultsPage?> GetActualResultsAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<ActualResultsSaveOutcome> SaveActualResultsAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, ActualResultsUpdate update, CancellationToken cancellationToken = default);
}

public sealed class ActualResultsService(IDistributionRepository distributionRepository, TimeProvider timeProvider) : IActualResultsService
{
    private sealed record State(
        IntendedResultsData Structure,
        ActualResultsHeaderEntity Header,
        Dictionary<Guid, string> SampleResults,
        Dictionary<Guid, string> MethodResults);

    public async Task<ActualResultsPage?> GetActualResultsAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(distributionParticipantId, monthlyDistributionSchemeId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        var isReadOnly = IsDeadlinePassed(state.Header.DeadlineDate, state.Header.FirstEditDate is null, timeProvider.GetLocalNow().DateTime);
        return BuildPage(distributionParticipantId, monthlyDistributionSchemeId, state, isReadOnly);
    }

    // Legacy deadline test shared by Save and the read-only page: a first save is cut off at 23:00 on
    // the deadline date (DataPortal_Insert), later saves 23 hours after the deadline (DataPortal_Update).
    private static bool IsDeadlinePassed(DateTime deadline, bool isNew, DateTime now) =>
        deadline.AddHours(23) < now || (isNew && deadline.Date.AddHours(23) <= now);

    // Legacy ButtonSave_Click / ButtonApply_Click (identical apart from where the user lands):
    //   If IsDirty Then
    //     If DeadlineDate.AddHours(23) >= Now Then
    //       If IsNew Then Save()  Else <Audit.aspx, which saves with the reason>
    //     Else <"Save Failed: No results can be saved after the deadline date.">
    //   Else (nothing to save)
    // A result row that has no stored row yet is a new child, so it makes the set dirty even when the
    // user typed nothing - exactly why legacy's first Save on an untouched page still records results.
    public async Task<ActualResultsSaveOutcome> SaveActualResultsAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, ActualResultsUpdate update, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(distributionParticipantId, monthlyDistributionSchemeId, cancellationToken);
        if (state is null)
        {
            return ActualResultsSaveOutcome.Of(ActualResultsSaveStatus.NotFound);
        }

        var postedSamples = new Dictionary<Guid, string>();
        foreach (var value in update.SampleValues)
        {
            postedSamples[value.SampleResultItemId] = value.Result ?? string.Empty;
        }

        var postedMethods = new Dictionary<Guid, string>();
        foreach (var item in update.MethodItems)
        {
            postedMethods[item.TestMethodItemId] = item.Result ?? string.Empty;
        }

        var errors = CollectErrors(state.Structure, postedSamples, postedMethods);
        if (errors.Count > 0)
        {
            return new ActualResultsSaveOutcome(ActualResultsSaveStatus.ValidationFailed, errors);
        }

        var isNew = state.Header.FirstEditDate is null;
        var methodItems = ResolveWrites(state.Structure.TestMethodItems.Select(m => m.TestMethodItemId), state.MethodResults, postedMethods);
        var sampleItems = ResolveWrites(state.Structure.SampleResultItems.Select(s => s.SampleResultItemId), state.SampleResults, postedSamples);

        var dirty = isNew || methodItems.Count > 0 || sampleItems.Count > 0;
        if (!dirty)
        {
            return ActualResultsSaveOutcome.Of(ActualResultsSaveStatus.NothingToSave);
        }

        var now = timeProvider.GetLocalNow().DateTime;
        var deadline = state.Header.DeadlineDate;
        if (IsDeadlinePassed(deadline, isNew, now))
        {
            return ActualResultsSaveOutcome.Of(ActualResultsSaveStatus.DeadlinePassed);
        }

        if (!isNew && string.IsNullOrWhiteSpace(update.AuditWhy))
        {
            return new ActualResultsSaveOutcome(ActualResultsSaveStatus.AuditReasonRequired, new Dictionary<Guid, string>(), AuditObjectName(state.Header));
        }

        var ratingSampleIds = new List<Guid>();
        Guid? defaultRatingId = null;
        if (isNew && state.Structure.Header.StoreRatings)
        {
            defaultRatingId = await DefaultRatingIdAsync(cancellationToken);
            ratingSampleIds.AddRange(state.Structure.Samples.Select(s => s.SampleId));
        }

        await distributionRepository.SaveActualResultsAsync(
            new ActualResultsWrite(
                distributionParticipantId,
                isNew,
                isNew ? now : state.Header.FirstEditDate!.Value,
                now,
                methodItems,
                sampleItems,
                ratingSampleIds,
                defaultRatingId,
                update.AuditWho,
                isNew ? string.Empty : update.AuditWhy),
            cancellationToken);

        return ActualResultsSaveOutcome.Of(ActualResultsSaveStatus.Saved);
    }

    private async Task<Guid> DefaultRatingIdAsync(CancellationToken cancellationToken)
    {
        var ratings = await distributionRepository.GetRatingIdsAsync(cancellationToken);
        if (ratings.Count == 0)
        {
            throw new InvalidOperationException("No Default Rating in database.");
        }

        return ratings[0];
    }

    private async Task<State?> LoadAsync(Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        var actual = await distributionRepository.GetActualResultsAsync(distributionParticipantId, cancellationToken);
        if (actual is null)
        {
            return null;
        }

        var structure = await distributionRepository.GetIntendedResultsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (structure is null)
        {
            return null;
        }

        return new State(
            structure,
            actual.Header,
            actual.SampleResults.ToDictionary(r => r.SampleResultItemId, r => r.Result ?? string.Empty),
            actual.MethodItemResults.ToDictionary(r => r.TestMethodItemId, r => r.Result ?? string.Empty));
    }

    // Items that must be written: no stored row yet (insert), or a stored row whose value changed.
    private static List<ActualResultWriteItem> ResolveWrites(
        IEnumerable<Guid> structureItemIds, Dictionary<Guid, string> stored, Dictionary<Guid, string> posted)
    {
        var writes = new List<ActualResultWriteItem>();
        foreach (var id in structureItemIds)
        {
            var exists = stored.TryGetValue(id, out var current);
            var value = posted.TryGetValue(id, out var postedValue) ? postedValue : current ?? string.Empty;

            if (!exists)
            {
                writes.Add(new ActualResultWriteItem(id, value, IsInsert: true));
            }
            else if (!string.Equals(value, current, StringComparison.Ordinal))
            {
                writes.Add(new ActualResultWriteItem(id, value, IsInsert: false));
            }
        }

        return writes;
    }

    // Legacy RegularExpressionValidator per regex-format item ("^..." FormatString); dropdown items
    // have no validator. Keyed by SampleResultItemId / TestMethodItemId.
    private static Dictionary<Guid, string> CollectErrors(
        IntendedResultsData structure, Dictionary<Guid, string> postedSamples, Dictionary<Guid, string> postedMethods)
    {
        var errors = new Dictionary<Guid, string>();
        var resultItems = structure.TestResultItems.ToDictionary(r => r.TestResultItemId);

        foreach (var sri in structure.SampleResultItems)
        {
            if (postedSamples.TryGetValue(sri.SampleResultItemId, out var value) && resultItems.TryGetValue(sri.TestResultItemId, out var item))
            {
                var message = IntendedResultsValidator.ValidateValue(item.FormatString, item.ReturnValueType, value);
                if (message is not null)
                {
                    errors[sri.SampleResultItemId] = message;
                }
            }
        }

        foreach (var method in structure.TestMethodItems)
        {
            if (postedMethods.TryGetValue(method.TestMethodItemId, out var value))
            {
                var message = IntendedResultsValidator.ValidateValue(method.FormatString, method.ReturnValueType, value);
                if (message is not null)
                {
                    errors[method.TestMethodItemId] = message;
                }
            }
        }

        return errors;
    }

    // Legacy ActualResultsSet.AuditObjectName(), shown as the heading on Audit.aspx.
    private static string AuditObjectName(ActualResultsHeaderEntity header) =>
        $"Results Received for {header.Distribution} {header.SchemeIdentifier}: {header.SchemeName}";

    private static ActualResultsPage BuildPage(Guid distributionParticipantId, Guid monthlyDistributionSchemeId, State state, bool isReadOnly)
    {
        var structure = state.Structure;
        var sampleTests = structure.SampleTests.ToDictionary(st => (st.SampleId, st.TestId));
        var sampleResultItems = structure.SampleResultItems.ToDictionary(sri => (sri.SampleTestId, sri.TestResultItemId));

        var tests = new List<ActualResultsTest>();
        foreach (var test in structure.Tests.OrderBy(t => t.Order))
        {
            var resultItems = structure.TestResultItems
                .Where(r => r.TestId == test.TestId)
                .OrderBy(r => r.Order)
                .ToList();

            var samples = new List<ActualResultsSampleRow>();
            foreach (var sample in structure.Samples)
            {
                if (!sampleTests.TryGetValue((sample.SampleId, test.TestId), out var sampleTest) || !sampleTest.IsApplicable)
                {
                    continue;
                }

                var values = new List<ActualResultsSampleValue>();
                foreach (var testResultItemId in resultItems.Select(r => r.TestResultItemId))
                {
                    if (sampleResultItems.TryGetValue((sampleTest.SampleTestId, testResultItemId), out var sri))
                    {
                        values.Add(new ActualResultsSampleValue(
                            sri.SampleResultItemId, testResultItemId, state.SampleResults.GetValueOrDefault(sri.SampleResultItemId, string.Empty)));
                    }
                }

                samples.Add(new ActualResultsSampleRow(sample.SampleId, sample.SampleNumber ?? string.Empty, values));
            }

            // Legacy: a Test with no applicable Sample is not rendered at all.
            if (samples.Count == 0)
            {
                continue;
            }

            var methodItems = structure.TestMethodItems
                .Where(m => m.TestId == test.TestId)
                .OrderBy(m => m.Order)
                .Select(m => new ActualResultsMethodItem(
                    m.TestMethodItemId, m.Name ?? string.Empty, m.FormatString ?? string.Empty, m.ReturnValueType ?? string.Empty,
                    state.MethodResults.GetValueOrDefault(m.TestMethodItemId, string.Empty)))
                .ToList();

            tests.Add(new ActualResultsTest(
                test.TestId,
                test.TestName ?? string.Empty,
                [.. resultItems.Select(r => new ActualResultsResultItem(r.TestResultItemId, r.Name ?? string.Empty, r.FormatString ?? string.Empty, r.ReturnValueType ?? string.Empty))],
                methodItems,
                samples));
        }

        var header = state.Header;
        return new ActualResultsPage(
            distributionParticipantId,
            monthlyDistributionSchemeId,
            $"{header.Distribution} {header.SchemeIdentifier}: {header.SchemeName}",
            AuditObjectName(header),
            header.DeadlineDate,
            header.FirstEditDate is null,
            tests,
            isReadOnly);
    }
}
