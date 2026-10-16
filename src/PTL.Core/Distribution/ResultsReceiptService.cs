namespace PTL.Core.Distribution;

// One participant's posted Extra Documents tick and Returned to Participant tick on Save/Apply -
// legacy ticks each checkbox through an instant WebForms autopostback; here the whole form is
// evaluated when Save/Apply is submitted, with the same persisted outcome.
public sealed record ResultsReceiptParticipantUpdate(
    Guid DistributionParticipantId,
    bool ExtraDocumentsReturned,
    bool ReturnedChecked);

public sealed record ResultsReceiptSaveOutcome(bool Success);

public interface IResultsReceiptService
{
    Task<ResultsReceiptData?> GetResultsReceiptAsync(int yearId, int monthId, CancellationToken cancellationToken = default);

    Task<ResultsReceiptSaveOutcome> SaveResultsReceiptAsync(
        int yearId, int monthId, IReadOnlyList<ResultsReceiptParticipantUpdate> updates, CancellationToken cancellationToken = default);

    Task<SampleReceiptPage?> GetSampleReceiptAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<SampleReceiptSaveOutcome> SaveSampleReceiptAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, IReadOnlyList<SampleReceiptUpdate> updates, CancellationToken cancellationToken = default);
}

public sealed class ResultsReceiptService(IDistributionRepository distributionRepository, TimeProvider timeProvider) : IResultsReceiptService
{
    public async Task<ResultsReceiptData?> GetResultsReceiptAsync(int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        var data = await distributionRepository.GetResultsReceiptAsync(yearId, monthId, cancellationToken);
        if (data is null)
        {
            return null;
        }

        foreach (var scheme in data.Schemes)
        {
            foreach (var participant in scheme.Participants)
            {
                // Legacy: a participant without their own deadline shows the scheme's deadline.
                participant.DeadlineDate = participant.DeadlineDate is { } own && own != default ? own : scheme.DeadlineDate;

                // Legacy: schemes that do not require assessment never show an unaccounted-samples status.
                if (!scheme.RequiresAssessment)
                {
                    participant.HasUnaccountedSamples = false;
                }
            }
        }

        return data;
    }

    // Legacy CustomCheckBox_CheckChanged sets ExtraDocumentsReturned; cbReturned_CheckChanged stamps
    // today's date as ReturnedDate (assessment schemes only, and only while it is not yet set), and
    // DistributionParticipantCollection.Update saves only the participants that changed.
    public async Task<ResultsReceiptSaveOutcome> SaveResultsReceiptAsync(
        int yearId, int monthId, IReadOnlyList<ResultsReceiptParticipantUpdate> updates, CancellationToken cancellationToken = default)
    {
        var data = await GetResultsReceiptAsync(yearId, monthId, cancellationToken);
        if (data is null)
        {
            return new ResultsReceiptSaveOutcome(false);
        }

        // Last update wins on duplicate ids; ToDictionary would throw on a crafted request.
        var updatesById = new Dictionary<Guid, ResultsReceiptParticipantUpdate>();
        foreach (var u in updates)
        {
            updatesById[u.DistributionParticipantId] = u;
        }
        var dirty = new List<ResultsReceiptParticipantEntity>();
        var today = timeProvider.GetLocalNow().Date;

        foreach (var scheme in data.Schemes)
        {
            foreach (var participant in scheme.Participants)
            {
                if (updatesById.TryGetValue(participant.DistributionParticipantId, out var update)
                    && ApplyParticipantUpdate(participant, update, scheme.RequiresAssessment, today))
                {
                    dirty.Add(participant);
                }
            }
        }

        if (dirty.Count > 0)
        {
            await distributionRepository.UpdateResultsReceiptAsync(dirty, cancellationToken);
        }

        return new ResultsReceiptSaveOutcome(true);
    }

    // True when the participant's stored values changed.
    private static bool ApplyParticipantUpdate(
        ResultsReceiptParticipantEntity participant, ResultsReceiptParticipantUpdate update, bool requiresAssessment, DateTime today)
    {
        var changed = false;

        if (participant.ExtraDocumentsReturned != update.ExtraDocumentsReturned)
        {
            participant.ExtraDocumentsReturned = update.ExtraDocumentsReturned;
            changed = true;
        }

        if (requiresAssessment && update.ReturnedChecked && participant.ReturnedDate is null)
        {
            participant.ReturnedDate = today;
            changed = true;
        }

        return changed;
    }

    public async Task<SampleReceiptPage?> GetSampleReceiptAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var data = await distributionRepository.GetSampleReceiptAsync(distributionParticipantId, monthlyDistributionSchemeId, cancellationToken);
        return data is null ? null : ToPage(data);
    }

    // Legacy ButtonSave_Click / ButtonApply_Click -> TestCollection.Save(); the controls are disabled
    // (so nothing can be posted) when the page is read-only, which is re-enforced here.
    public async Task<SampleReceiptSaveOutcome> SaveSampleReceiptAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, IReadOnlyList<SampleReceiptUpdate> updates, CancellationToken cancellationToken = default)
    {
        var data = await distributionRepository.GetSampleReceiptAsync(distributionParticipantId, monthlyDistributionSchemeId, cancellationToken);
        if (data is null)
        {
            return new SampleReceiptSaveOutcome(SampleReceiptSaveStatus.NotFound);
        }

        if (IsReadOnly(data))
        {
            return new SampleReceiptSaveOutcome(SampleReceiptSaveStatus.ReadOnly);
        }

        var updatesById = new Dictionary<Guid, SampleReceiptUpdate>();
        foreach (var u in updates)
        {
            updatesById[u.SampleReceiptId] = u;
        }

        var today = timeProvider.GetLocalNow().Date;
        var changed = new List<SampleReceiptSampleEntity>();
        foreach (var sample in data.Samples)
        {
            if (updatesById.TryGetValue(sample.SampleReceiptId, out var update) && ApplyTicks(sample, update, today))
            {
                changed.Add(sample);
            }
        }

        if (changed.Count > 0)
        {
            await distributionRepository.UpdateSampleReceiptAsync(changed, cancellationToken);
        }

        return new SampleReceiptSaveOutcome(SampleReceiptSaveStatus.Saved);
    }

    // Legacy: controls are disabled when there are no Tests or the scheme is ready for the assessor.
    private static bool IsReadOnly(SampleReceiptData data) =>
        data.Tests.Count == 0 || data.Scheme.IsReadyForAssessor is not null;

    private static SampleReceiptPage ToPage(SampleReceiptData data)
    {
        var samplesByTest = data.Samples.ToLookup(s => s.TestId);

        // Legacy only renders a Test that has at least one Sample.
        var tests = data.Tests
            .Select(t => new SampleReceiptTest(
                t.TestId,
                t.TestType,
                [.. samplesByTest[t.TestId].Select(s => new SampleReceiptSample(
                    s.SampleReceiptId, s.SampleNumber, s.SubmissionDate is not null, s.NonSubmissionDate is not null, s.NonCapableDate is not null))]))
            .Where(t => t.Samples.Count > 0)
            .ToList();

        return new SampleReceiptPage(
            $"{data.Scheme.DistributionReferenceFull} : {data.Scheme.SchemeIdentifier} {data.Scheme.SchemeName}",
            $"Labcode {data.Lab.LabCode}: {data.Lab.LabName}",
            IsReadOnly(data),
            tests);
    }

    private enum SampleReceiptKind
    {
        None,
        Submitted,
        NotSubmitted,
        NotInRepertoire,
    }

    // Legacy Checkbox_CheckChanged: the three ticks are mutually exclusive - ticking one stamps today's
    // date and clears the other two; unticking it clears its date. A form posts every tick at once, so
    // the tick that differs from what is stored is the one the user just made.
    private static bool ApplyTicks(SampleReceiptSampleEntity sample, SampleReceiptUpdate update, DateTime today)
    {
        var storedSubmitted = sample.SubmissionDate is not null;
        var storedNotSubmitted = sample.NonSubmissionDate is not null;
        var storedNotInRepertoire = sample.NonCapableDate is not null;

        if (update.Submitted == storedSubmitted && update.NotSubmitted == storedNotSubmitted && update.NotInRepertoire == storedNotInRepertoire)
        {
            return false;
        }

        var chosen = NewlyTicked(update, storedSubmitted, storedNotSubmitted, storedNotInRepertoire);

        var submissionDate = chosen == SampleReceiptKind.Submitted ? sample.SubmissionDate ?? today : (DateTime?)null;
        var nonSubmissionDate = chosen == SampleReceiptKind.NotSubmitted ? sample.NonSubmissionDate ?? today : (DateTime?)null;
        var nonCapableDate = chosen == SampleReceiptKind.NotInRepertoire ? sample.NonCapableDate ?? today : (DateTime?)null;

        var changed = submissionDate != sample.SubmissionDate
            || nonSubmissionDate != sample.NonSubmissionDate
            || nonCapableDate != sample.NonCapableDate;

        sample.SubmissionDate = submissionDate;
        sample.NonSubmissionDate = nonSubmissionDate;
        sample.NonCapableDate = nonCapableDate;
        return changed;
    }

    // The tick that differs from what is stored is the one just made; failing that, the first tick set.
    private static SampleReceiptKind NewlyTicked(SampleReceiptUpdate update, bool storedSubmitted, bool storedNotSubmitted, bool storedNotInRepertoire)
    {
        if (update.Submitted && !storedSubmitted)
        {
            return SampleReceiptKind.Submitted;
        }

        if (update.NotSubmitted && !storedNotSubmitted)
        {
            return SampleReceiptKind.NotSubmitted;
        }

        if (update.NotInRepertoire && !storedNotInRepertoire)
        {
            return SampleReceiptKind.NotInRepertoire;
        }

        if (update.Submitted)
        {
            return SampleReceiptKind.Submitted;
        }

        if (update.NotSubmitted)
        {
            return SampleReceiptKind.NotSubmitted;
        }

        return update.NotInRepertoire ? SampleReceiptKind.NotInRepertoire : SampleReceiptKind.None;
    }
}
