namespace PTL.Core.Distribution;

// Sample Numbers (legacy Scheduling/SampleNumbers.aspx) read/save behaviour, split out of
// DistributionService's scheduling concerns.
public interface ISampleNumbersService
{
    Task<SampleNumbersPage?> GetSampleNumbersAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<SampleNumbersSaveOutcome> SaveSampleNumbersAsync(
        Guid monthlyDistributionSchemeId, SampleNumbersUpdate update, CancellationToken cancellationToken = default);
}

public sealed class SampleNumbersService(
    IDistributionRepository distributionRepository,
    PTL.Core.Scheme.ISchemeService schemeService) : ISampleNumbersService
{
    public async Task<SampleNumbersPage?> GetSampleNumbersAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var scheme = await distributionRepository.GetMonthlyDistributionSchemeAsync(monthlyDistributionSchemeId, cancellationToken);
        if (scheme is null)
        {
            return null;
        }

        var data = await distributionRepository.GetIntendedResultsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (data is null)
        {
            return null;
        }

        // Legacy SampleNumbers.aspx.vb: when the distribution has no sample numbers yet it builds
        // the whole structure from the Scheme and shows it. Legacy held that in Session and
        // re-created it on every fresh GET, which re-consumed sample numbers on each refresh;
        // persisting it as a draft instead gives the same screen without the sequence drift.
        if (data.Samples.Count == 0 && !data.Header.HasSampleNumbersDefined)
        {
            data = await CreateStructureAsync(scheme, data, cancellationToken);
        }

        return BuildPage(scheme, data);
    }

    private async Task<IntendedResultsData> CreateStructureAsync(
        MonthlyDistributionSchemeEntity scheme, IntendedResultsData data, CancellationToken cancellationToken)
    {
        var fullScheme = await schemeService.GetSchemeAsync(scheme.SchemeId, cancellationToken)
            ?? throw new DistributionStructureException(
                $"Scheme {scheme.SchemeIdentifier} could not be loaded, so Sample Numbers cannot be defined for {scheme.DistributionReferenceFull}.");

        var structure = DistributionStructureBuilder.Build(scheme.MonthlyDistributionSchemeId, fullScheme);

        await distributionRepository.CreateDistributionStructureAsync(
            scheme.MonthlyDistributionSchemeId,
            fullScheme.ScheduleId,
            DistributionStructureBuilder.BuildSampleNumberDate(scheme.YearId, scheme.MonthId),
            structure,
            cancellationToken);

        // Re-read whether we created it or lost the race - either way the database is now the
        // single source of truth for what the screen shows.
        return await distributionRepository.GetIntendedResultsAsync(scheme.MonthlyDistributionSchemeId, cancellationToken) ?? data;
    }

    // Legacy LoadObjectFromForm: the whole page is validated as one ValidationGroup, then every
    // sample row, every Sample Allocation tick box and the Comments box are pushed back onto the
    // CSLA object and HasSampleNumbersDefined/HasIntendedResults are both forced true. The object
    // is saved ONLY when something actually changed (If mIntendedResults.IsDirty), and CSLA then
    // writes only the dirty children - this matters because spuSampleTest clears tblSampleReceipt's
    // submission dates, so writing an unchanged allocation would silently reset a receipt.
    // Changing a Tabulation Description on an existing sample is an AuditableProperty, so legacy
    // diverts to Audit.aspx for a mandatory reason before the save is allowed to proceed.
    // A sample id that is not already on this distribution is ignored, exactly as legacy (which
    // only ever iterates the samples it fetched).
    public async Task<SampleNumbersSaveOutcome> SaveSampleNumbersAsync(
        Guid monthlyDistributionSchemeId, SampleNumbersUpdate update, CancellationToken cancellationToken = default)
    {
        var pageErrors = SampleNumbersValidator.ValidateComments(update.Comments);
        var fieldErrors = CollectFieldErrors(update.Samples);

        if (pageErrors.Count > 0 || fieldErrors.Count > 0)
        {
            return SampleNumbersSaveOutcome.Failed(pageErrors, fieldErrors);
        }

        var data = await distributionRepository.GetIntendedResultsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (data is null)
        {
            return SampleNumbersSaveOutcome.Failed(pageErrors, fieldErrors);
        }

        var changes = CollectChanges(update, data);

        var header = data.Header;
        var headerChanged = !Matches(header.Comments, update.Comments)
            || !header.HasSampleNumbersDefined
            || !header.HasIntendedResults;

        // Legacy: nothing dirty means Save() is never called at all - no spuIntendedResults, no
        // spuSample, no spuSampleTest and no spiSetupSampleReceipt. The user is simply redirected.
        if (!headerChanged && changes.Samples.Count == 0)
        {
            return SampleNumbersSaveOutcome.Succeeded();
        }

        // Legacy IsAuditDirty: Audit.aspx collects a mandatory reason BEFORE the save runs.
        if (changes.AuditedSampleIds.Count > 0 && string.IsNullOrWhiteSpace(update.AuditWhy))
        {
            return SampleNumbersSaveOutcome.NeedsAuditReason(AuditObjectNameValue);
        }

        header.Comments = update.Comments;
        header.HasSampleNumbersDefined = true;
        header.HasIntendedResults = true;

        await distributionRepository.UpdateSampleNumbersAsync(
            new SampleNumbersWrite(
                header,
                changes.Samples,
                changes.SampleTests,
                changes.AuditedSampleIds,
                update.AuditWho,
                update.AuditWhy,
                SetupSampleReceipt: header.RequiresAssessment),
            cancellationToken);

        return SampleNumbersSaveOutcome.Succeeded();
    }

    private static Dictionary<Guid, IReadOnlyList<SampleNumbersError>> CollectFieldErrors(
        IReadOnlyList<SampleNumberRowUpdate> postedSamples)
    {
        var fieldErrors = new Dictionary<Guid, IReadOnlyList<SampleNumbersError>>();
        foreach (var sample in postedSamples)
        {
            var errors = SampleNumbersValidator.ValidateSample(sample);
            if (errors.Count > 0)
            {
                fieldErrors[sample.SampleId] = errors;
            }
        }

        return fieldErrors;
    }

    // The dirty subset of one legacy IntendedResultsSet.Save(): only the samples and sample tests
    // whose values actually differ from what was fetched.
    private sealed record SampleChanges(
        List<DistributionSampleEntity> Samples,
        List<DistributionSampleTestEntity> SampleTests,
        List<Guid> AuditedSampleIds);

    private static SampleChanges CollectChanges(SampleNumbersUpdate update, IntendedResultsData data)
    {
        var existingSamples = data.Samples.ToDictionary(s => s.SampleId);
        var existingSampleTests = data.SampleTests.ToDictionary(t => t.SampleTestId);

        // Legacy disables every tick box once an assessment scheme is with the assessor, so the
        // posted values for them are meaningless and must not be applied.
        var allocationsLocked = data.Header.RequiresAssessment && data.Header.IsReadyForAssessor is not null;

        var changes = new SampleChanges([], [], []);

        foreach (var posted in update.Samples)
        {
            if (!existingSamples.TryGetValue(posted.SampleId, out var sample))
            {
                continue;
            }

            List<DistributionSampleTestEntity> changedAllocations =
                allocationsLocked ? [] : CollectChangedAllocations(posted, existingSampleTests);

            var tabulationDescriptionChanged = !Matches(sample.TabulationDescription, posted.TabulationDescription);
            var sampleChanged = tabulationDescriptionChanged
                || !Matches(sample.SampleNumber, posted.SampleNumber)
                || !Matches(sample.InternalDescription, posted.InternalDescription)
                || !Matches(sample.ExternalDescription, posted.ExternalDescription)
                || !Matches(sample.TestConsultantsDescription, posted.TestConsultantsDescription);

            // Legacy Sample.IsDirty is "my own fields OR any of my sample tests", and a dirty
            // Sample always runs spuSample - so an allocation-only change still rewrites the row.
            if (!sampleChanged && changedAllocations.Count == 0)
            {
                continue;
            }

            sample.SampleNumber = posted.SampleNumber;
            sample.InternalDescription = posted.InternalDescription;
            sample.ExternalDescription = posted.ExternalDescription;
            sample.TabulationDescription = posted.TabulationDescription;
            sample.TestConsultantsDescription = posted.TestConsultantsDescription;
            changes.Samples.Add(sample);
            changes.SampleTests.AddRange(changedAllocations);

            if (tabulationDescriptionChanged)
            {
                changes.AuditedSampleIds.Add(sample.SampleId);
            }
        }

        return changes;
    }

    private static List<DistributionSampleTestEntity> CollectChangedAllocations(
        SampleNumberRowUpdate posted, Dictionary<Guid, DistributionSampleTestEntity> existingSampleTests)
    {
        var changedAllocations = new List<DistributionSampleTestEntity>();
        foreach (var allocation in posted.Allocations)
        {
            if (!existingSampleTests.TryGetValue(allocation.SampleTestId, out var sampleTest)
                || sampleTest.SampleId != posted.SampleId
                || sampleTest.IsApplicable == allocation.IsApplicable)
            {
                continue;
            }

            sampleTest.IsApplicable = allocation.IsApplicable;
            changedAllocations.Add(sampleTest);
        }

        return changedAllocations;
    }

    // Legacy IntendedResultsSet.AuditObjectName(), shown as the heading on Audit.aspx.
    private const string AuditObjectNameValue = "Intended Results Set";

    // tblSample columns are nullable, so a stored NULL and a posted "" are the same value.
    private static bool Matches(string? stored, string? posted) =>
        string.Equals(stored ?? string.Empty, posted ?? string.Empty, StringComparison.Ordinal);

    private static SampleNumbersPage BuildPage(MonthlyDistributionSchemeEntity scheme, IntendedResultsData data)
    {
        var sampleTestsBySample = data.SampleTests
            .GroupBy(t => t.SampleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var samples = data.Samples
            .Select(s => new SampleNumberRow(
                s.SampleId,
                s.SampleNumber ?? string.Empty,
                s.InternalDescription ?? string.Empty,
                s.ExternalDescription ?? string.Empty,
                s.TabulationDescription ?? string.Empty,
                s.TestConsultantsDescription ?? string.Empty,
                sampleTestsBySample.TryGetValue(s.SampleId, out var allocations)
                    ? [.. allocations.Select(a => new SampleAllocation(a.SampleTestId, a.TestId, a.IsApplicable))]
                    : []))
            .ToList();

        var criterionNamesByCategory = data.CriterionItems
            .GroupBy(c => c.CategoryItemId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Name ?? string.Empty).ToList());

        var tests = data.Tests
            .Select(t => new SampleNumbersTest(
                t.TestId,
                t.TestName ?? string.Empty,
                [.. data.TestResultItems.Where(r => r.TestId == t.TestId).Select(r => r.Name ?? string.Empty)],
                [.. data.TestMethodItems.Where(m => m.TestId == t.TestId).Select(m => m.Name ?? string.Empty)],
                [.. data.CategoryItems.Where(c => c.TestId == t.TestId).Select(c => BuildCategoryItemName(c, criterionNamesByCategory))]))
            .ToList();

        return new SampleNumbersPage(
            scheme.MonthlyDistributionSchemeId,
            scheme.DistributionReferenceFull,
            scheme.SchemeIdentifier,
            scheme.SchemeName,
            scheme.TotalSetsOfSamplesRequired,
            data.Header.Comments ?? string.Empty,
            data.Header.RequiresAssessment,
            data.Header.IsReadyForAssessor is not null,
            samples,
            tests);
    }

    // Legacy PopulateTabContainer: catItem.Name & " (" & catItem.CriteriaText & ")", where
    // CriteriaText is the comma-separated criterion names.
    private static string BuildCategoryItemName(
        DistributionCategoryItemEntity categoryItem, Dictionary<Guid, List<string>> criterionNamesByCategory)
    {
        var criteria = criterionNamesByCategory.TryGetValue(categoryItem.CategoryItemId, out var names)
            ? string.Join(", ", names)
            : string.Empty;

        return $"{categoryItem.Name} ({criteria})";
    }
}
