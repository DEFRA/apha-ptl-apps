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
                if (!updatesById.TryGetValue(participant.DistributionParticipantId, out var update))
                {
                    continue;
                }

                var changed = false;

                if (participant.ExtraDocumentsReturned != update.ExtraDocumentsReturned)
                {
                    participant.ExtraDocumentsReturned = update.ExtraDocumentsReturned;
                    changed = true;
                }

                if (scheme.RequiresAssessment && update.ReturnedChecked && participant.ReturnedDate is null)
                {
                    participant.ReturnedDate = today;
                    changed = true;
                }

                if (changed)
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
}
