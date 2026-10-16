namespace PTL.Core.Distribution;

// Distribution Participants screen (legacy Scheduling/DistributionParticipantList.aspx) - view,
// Change Deadline Date, and Repeat Distribution. The audit-trail acceptance criteria (Actor/Old
// Date/New Date history) raised in the user story has NO legacy equivalent - confirmed [NEEDS
// BUSINESS DECISION], not implemented here. DeadlineChangeReason is persisted as legacy does: a
// single overwritable column, never a history.
public interface IDistributionParticipantsService
{
    Task<DistributionParticipantsData?> GetParticipantsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<ChangeDeadlineOutcome> ChangeDeadlineAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, DateTime? newDeadlineDate, string? reason, CancellationToken cancellationToken = default);

    Task<RepeatDistributionOutcome> RepeatDistributionAsync(
        Guid monthlyDistributionSchemeId, IReadOnlyList<ParticipantSelection> selections, CancellationToken cancellationToken = default);
}

public sealed record ChangeDeadlineOutcome(bool Success, string? NewDeadlineDateError, string? ReasonError)
{
    public static ChangeDeadlineOutcome Succeeded() => new(true, null, null);
}

public sealed record RepeatDistributionOutcome(bool Success, string? Error)
{
    public static RepeatDistributionOutcome Succeeded() => new(true, null);

    public static RepeatDistributionOutcome Failed(string error) => new(false, error);
}

public sealed class DistributionParticipantsService(IDistributionRepository distributionRepository) : IDistributionParticipantsService
{
    // Legacy ValidatorDistributionDateRequired/RequiredFieldValidator2 - both the new date and the
    // reason are mandatory on this screen (verbatim legacy ToolTip text).
    private const string NewDeadlineDateRequiredMessage = "Deadline Date required.";
    private const string ReasonRequiredMessage = "A reason for changing the deadline date is required.";

    // CSLA StringMaxLength("DeadlineChangeReason", 255) on DistributionParticipant.vb.
    private const int ReasonMaxLength = 255;

    public Task<DistributionParticipantsData?> GetParticipantsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        distributionRepository.GetDistributionParticipantsAsync(monthlyDistributionSchemeId, cancellationToken);

    public async Task<ChangeDeadlineOutcome> ChangeDeadlineAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, DateTime? newDeadlineDate, string? reason, CancellationToken cancellationToken = default)
    {
        // Legacy SetNewDeadlineDate silently no-ops when the reason is blank - CausesValidation on
        // the Save link means this is only reachable with both already present, but validate again
        // server-side since nothing stops a direct post.
        var reasonError = string.IsNullOrWhiteSpace(reason) ? ReasonRequiredMessage : null;
        var dateError = newDeadlineDate is null ? NewDeadlineDateRequiredMessage : null;

        if (reasonError is not null || dateError is not null)
        {
            return new ChangeDeadlineOutcome(false, dateError, reasonError);
        }

        if (reason!.Length > ReasonMaxLength)
        {
            return new ChangeDeadlineOutcome(false, null, $"Input can not exceed {ReasonMaxLength} characters");
        }

        var data = await distributionRepository.GetDistributionParticipantsAsync(monthlyDistributionSchemeId, cancellationToken);
        var participant = data?.Participants.FirstOrDefault(p => p.DistributionParticipantId == distributionParticipantId);
        if (participant is null)
        {
            return new ChangeDeadlineOutcome(false, null, null);
        }

        participant.DeadlineDate = newDeadlineDate!.Value;
        participant.DeadlineChangeReason = reason;

        await distributionRepository.UpdateParticipantDeadlineAsync(participant, cancellationToken);
        return ChangeDeadlineOutcome.Succeeded();
    }

    // Legacy MonthlyDistribution.RepeatDistribution: a new MonthlyDistributionScheme is created
    // sharing the same DistributionReference, suffixed with the next letter in sequence ('a', 'b',
    // ...) among every scheme in the month already sharing that reference - ChrW(count + 96).
    public async Task<RepeatDistributionOutcome> RepeatDistributionAsync(
        Guid monthlyDistributionSchemeId, IReadOnlyList<ParticipantSelection> selections, CancellationToken cancellationToken = default)
    {
        if (selections.Count == 0)
        {
            return RepeatDistributionOutcome.Failed(
                "Please tick at least one participant to be included in the repeat of this distribution.");
        }

        var data = await distributionRepository.GetDistributionParticipantsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (data is null)
        {
            return RepeatDistributionOutcome.Failed("This distribution could not be found.");
        }

        var month = await distributionRepository.GetMonthlyDistributionSchedulesAsync(data.Scheme.YearId, data.Scheme.MonthId, cancellationToken);
        var sameReferenceCount = month?.Schemes.Count(s => s.DistributionReference == data.Scheme.DistributionReference) ?? 1;
        var suffix = ((char)('a' + sameReferenceCount - 1)).ToString();

        var newScheme = new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = Guid.NewGuid(),
            MonthlyDistributionId = data.Scheme.MonthlyDistributionId,
            SchemeId = data.Scheme.SchemeId,
            YearId = data.Scheme.YearId,
            MonthId = data.Scheme.MonthId,
            SchemeIdentifier = data.Scheme.SchemeIdentifier,
            SchemeName = data.Scheme.SchemeName,
            DistributionReference = data.Scheme.DistributionReference,
            DistributionReferenceSuffix = suffix,
            ScheduleCode = data.Scheme.ScheduleCode,
            DistributionDate = data.Scheme.DistributionDate,
            OverseasPostingDate = data.Scheme.OverseasPostingDate,
            DeadlineDate = data.Scheme.DeadlineDate,
            ResultsIssueTargetDate = data.Scheme.ResultsIssueTargetDate,
            Comments = string.Empty,
            HasIntendedResults = false,
            HasSampleNumbersDefined = false,
            // Legacy NewMonthlyDistributionScheme: IsCancelled starts equal to IsAsAvailable.
            IsCancelled = data.Scheme.IsAsAvailable,
            IsAsAvailable = data.Scheme.IsAsAvailable,
            StoreRatings = data.Scheme.StoreRatings,
            SchemeVersionDate = data.Scheme.SchemeVersionDate,
        };

        var participantsById = data.Participants.ToDictionary(p => p.DistributionParticipantId);
        var additionalByParticipantSchemeId = data.AdditionalParticipants.ToDictionary(p => p.ParticipantSchemeId);

        var newParticipants = new List<DistributionParticipantEntity>();
        foreach (var selection in selections)
        {
            if (selection.IsAdditional)
            {
                if (!additionalByParticipantSchemeId.TryGetValue(selection.Id, out var additional))
                {
                    continue;
                }

                newParticipants.Add(new DistributionParticipantEntity
                {
                    DistributionParticipantId = Guid.NewGuid(),
                    ParticipantId = additional.ParticipantId,
                    LabCode = additional.LabCode,
                    LabName = additional.LabName,
                    MonthlyDistributionSchemeId = newScheme.MonthlyDistributionSchemeId,
                    DeadlineDate = null,
                    SchemeDeadlineDate = newScheme.DeadlineDate,
                    DeadlineChangeReason = string.Empty,
                    ContractId = additional.ContractId,
                    ParticipantSchemeId = additional.ParticipantSchemeId,
                    AdditionalParticipant = true,
                    NumberOfSetsRequired = additional.NumberOfSetsRequired,
                });
            }
            else
            {
                if (!participantsById.TryGetValue(selection.Id, out var existing))
                {
                    continue;
                }

                newParticipants.Add(new DistributionParticipantEntity
                {
                    DistributionParticipantId = Guid.NewGuid(),
                    ParticipantId = existing.ParticipantId,
                    LabCode = existing.LabCode,
                    LabName = existing.LabName,
                    MonthlyDistributionSchemeId = newScheme.MonthlyDistributionSchemeId,
                    DeadlineDate = null,
                    SchemeDeadlineDate = newScheme.DeadlineDate,
                    DeadlineChangeReason = string.Empty,
                    ContractId = existing.ContractId,
                    ParticipantSchemeId = existing.ParticipantSchemeId,
                    AdditionalParticipant = existing.AdditionalParticipant,
                    NumberOfSetsRequired = existing.NumberOfSetsRequired,
                });
            }
        }

        await distributionRepository.RepeatDistributionAsync(newScheme, newParticipants, cancellationToken);
        return RepeatDistributionOutcome.Succeeded();
    }
}
