using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed partial class FakeDistributionRepository : IDistributionRepository
{
    public List<DistributionMonthSummaryEntity> Summaries { get; } = [];

    public List<DistributionMonthYearEntity> Years { get; } = [];

    // Keyed by (yearId, monthId) - null means "not initialised", matching GetMonthlyDistributionSchedulesAsync's contract.
    public Dictionary<(int YearId, int MonthId), (Guid MonthlyDistributionId, List<MonthlyDistributionSchemeEntity> Schemes)> Schedules { get; } = [];

    public List<MonthlyDistributionSchemeEntity> UpdatedSchemes { get; } = [];

    public Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DistributionMonthSummaryEntity>>(Summaries);

    public Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DistributionMonthYearEntity>>(Years);

    public Task<(Guid MonthlyDistributionId, IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes)?> GetMonthlyDistributionSchedulesAsync(
        int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        if (!Schedules.TryGetValue((yearId, monthId), out var schedule))
        {
            return Task.FromResult<(Guid, IReadOnlyList<MonthlyDistributionSchemeEntity>)?>(null);
        }

        return Task.FromResult<(Guid, IReadOnlyList<MonthlyDistributionSchemeEntity>)?>((schedule.MonthlyDistributionId, schedule.Schemes));
    }

    public Task UpdateMonthlyDistributionSchemesAsync(IReadOnlyList<MonthlyDistributionSchemeEntity> schemes, CancellationToken cancellationToken = default)
    {
        UpdatedSchemes.AddRange(schemes);
        return Task.CompletedTask;
    }

    public Dictionary<Guid, MonthlyDistributionSchemeEntity> SchemesById { get; } = [];

    public Dictionary<Guid, IntendedResultsData> IntendedResults { get; } = [];

    public List<SampleNumbersWrite> SavedSampleNumbers { get; } = [];

    public Task<MonthlyDistributionSchemeEntity?> GetMonthlyDistributionSchemeAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemesById.GetValueOrDefault(monthlyDistributionSchemeId));

    public Task<IntendedResultsData?> GetIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(IntendedResults.GetValueOrDefault(monthlyDistributionSchemeId));

    public Task UpdateSampleNumbersAsync(SampleNumbersWrite write, CancellationToken cancellationToken = default)
    {
        SavedSampleNumbers.Add(write);
        return Task.CompletedTask;
    }

    public List<(Guid SchemeId, Guid ScheduleId, DateTime SampleNumberDate, DistributionStructure Structure)> CreatedStructures { get; } = [];

    // Set to false to simulate losing the concurrent-create race.
    public bool CreateStructureSucceeds { get; set; } = true;

    // Optional: the IntendedResultsData GetIntendedResultsAsync should return after a create.
    public IntendedResultsData? IntendedResultsAfterCreate { get; set; }

    public Task<bool> CreateDistributionStructureAsync(
        Guid monthlyDistributionSchemeId,
        Guid scheduleId,
        DateTime sampleNumberDate,
        DistributionStructure structure,
        CancellationToken cancellationToken = default)
    {
        CreatedStructures.Add((monthlyDistributionSchemeId, scheduleId, sampleNumberDate, structure));
        if (IntendedResultsAfterCreate is not null)
        {
            IntendedResults[monthlyDistributionSchemeId] = IntendedResultsAfterCreate;
        }

        return Task.FromResult(CreateStructureSucceeds);
    }

    public List<IntendedResultsWrite> SavedIntendedResults { get; } = [];

    public Task UpdateIntendedResultsAsync(IntendedResultsWrite write, CancellationToken cancellationToken = default)
    {
        SavedIntendedResults.Add(write);
        return Task.CompletedTask;
    }

    public Dictionary<Guid, DistributionParticipantsData> ParticipantsByScheme { get; } = [];

    public Dictionary<Guid, List<CurrentDistributionData>> CurrentDistributionsByParticipant { get; } = [];

    public List<DistributionParticipantEntity> UpdatedParticipantDeadlines { get; } = [];

    public List<(MonthlyDistributionSchemeEntity NewScheme, IReadOnlyList<DistributionParticipantEntity> NewParticipants)> RepeatedDistributions { get; } = [];

    public Task<DistributionParticipantsData?> GetDistributionParticipantsAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ParticipantsByScheme.GetValueOrDefault(monthlyDistributionSchemeId));

    public Task<IReadOnlyList<CurrentDistributionData>> GetCurrentDistributionsAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CurrentDistributionData>>(CurrentDistributionsByParticipant.GetValueOrDefault(participantId) ?? []);

    public Task UpdateParticipantDeadlineAsync(DistributionParticipantEntity participant, CancellationToken cancellationToken = default)
    {
        UpdatedParticipantDeadlines.Add(participant);
        return Task.CompletedTask;
    }

    public Task RepeatDistributionAsync(
        MonthlyDistributionSchemeEntity newScheme, IReadOnlyList<DistributionParticipantEntity> newParticipants, CancellationToken cancellationToken = default)
    {
        RepeatedDistributions.Add((newScheme, newParticipants));
        return Task.CompletedTask;
    }

    public bool InitialiseMonthResult { get; set; } = true;

    public List<MonthSetupSchemeRow> SetupSchemeRows { get; } = [];

    public List<MonthSetupParticipantRow> SetupParticipantRows { get; } = [];

    public List<(int YearId, int MonthId)> InitialisedMonths { get; } = [];

    public MonthSetupPlan? LastMonthPlan { get; private set; }

    public Task<bool> InitialiseMonthAsync(int yearId, int monthId, MonthSetupPlanner planner, CancellationToken cancellationToken = default)
    {
        InitialisedMonths.Add((yearId, monthId));
        LastMonthPlan = planner(SetupSchemeRows, SetupParticipantRows);
        return Task.FromResult(InitialiseMonthResult);
    }

    public Dictionary<Guid, PreparationSheetData> PreparationSheets { get; } = [];

    public List<PreparationSchemeEntity> UpdatedPreparationSchemes { get; } = [];

    public Task<PreparationSheetData?> GetPreparationSheetAsync(Guid monthlyDistributionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PreparationSheets.GetValueOrDefault(monthlyDistributionId));

    public Task UpdatePreparationAsync(IReadOnlyList<PreparationSchemeEntity> schemes, CancellationToken cancellationToken = default)
    {
        UpdatedPreparationSchemes.AddRange(schemes);
        return Task.CompletedTask;
    }

    // Keyed by (yearId, monthId); a missing key means "no distribution for the month" (null).
    public Dictionary<(int YearId, int MonthId), ResultsReceiptData> ResultsReceipts { get; } = [];

    public List<ResultsReceiptParticipantEntity> UpdatedResultsReceiptParticipants { get; } = [];

    public Task<ResultsReceiptData?> GetResultsReceiptAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultsReceipts.GetValueOrDefault((yearId, monthId)));

    public Dictionary<Guid, IReadOnlyList<TabulationsSchemeEntity>> Tabulations { get; } = [];

    public List<Guid> TabulationRequests { get; } = [];

    public Task<IReadOnlyList<TabulationsSchemeEntity>> GetTabulationsAsync(Guid monthlyDistributionId, CancellationToken cancellationToken = default)
    {
        TabulationRequests.Add(monthlyDistributionId);
        return Task.FromResult(Tabulations.GetValueOrDefault(monthlyDistributionId) ?? []);
    }

    public Task UpdateResultsReceiptAsync(IReadOnlyList<ResultsReceiptParticipantEntity> participants, CancellationToken cancellationToken = default)
    {
        UpdatedResultsReceiptParticipants.AddRange(participants);
        return Task.CompletedTask;
    }

    // Keyed by (distributionParticipantId, monthlyDistributionSchemeId); a missing key means "not found" (null).
    public Dictionary<(Guid ParticipantId, Guid SchemeId), SampleReceiptData> SampleReceipts { get; } = [];

    public List<SampleReceiptSampleEntity> UpdatedSampleReceipts { get; } = [];

    public Task<SampleReceiptData?> GetSampleReceiptAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SampleReceipts.GetValueOrDefault((distributionParticipantId, monthlyDistributionSchemeId)));

    public Task UpdateSampleReceiptAsync(IReadOnlyList<SampleReceiptSampleEntity> samples, CancellationToken cancellationToken = default)
    {
        UpdatedSampleReceipts.AddRange(samples);
        return Task.CompletedTask;
    }

    // Keyed by distributionParticipantId; a missing key means "participant not returned" (null).
    public Dictionary<Guid, ActualResultsData> ActualResults { get; } = [];

    public List<Guid> RatingIds { get; } = [];

    public List<ActualResultsWrite> SavedActualResults { get; } = [];

    public Task<ActualResultsData?> GetActualResultsAsync(Guid distributionParticipantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ActualResults.GetValueOrDefault(distributionParticipantId));

    public Task<IReadOnlyList<Guid>> GetRatingIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(RatingIds);

    public Task SaveActualResultsAsync(ActualResultsWrite write, CancellationToken cancellationToken = default)
    {
        SavedActualResults.Add(write);
        return Task.CompletedTask;
    }

    // Keyed by (monthlyDistributionId, mode, week); a missing key means "no packaging header" (null).
    public Dictionary<(Guid MonthlyDistributionId, PackagingMode Mode, int Week), PackagingRawData> PackagingSheets { get; } = [];

    public List<(Guid MonthlyDistributionId, PackagingMode Mode, int Week)> PackagingSheetRequests { get; } = [];

    public List<(IReadOnlyList<PackagingProgressUpdate> Updates, IReadOnlyList<DistributionPostedEmailEntity> Emails)> PackagingUpdates { get; } = [];

    public List<DistributionPostedEmailEntity> UnsentPostedEmails { get; } = [];

    public int UnsentPostedEmailRequests { get; private set; }

    public List<(Guid Id, DateTime DateSent)> MarkedSentEmails { get; } = [];

    // Keyed by (participantId, schemeId) - an absent key returns null (no address row).
    public Dictionary<(Guid ParticipantId, Guid SchemeId), PackagingLabelAddressEntity> ParticipantLabelAddresses { get; } = [];

    public Dictionary<Guid, PackagingLabelAddressEntity> GroupLabelAddresses { get; } = [];

    public List<(Guid ParticipantId, Guid SchemeId)> ParticipantLabelRequests { get; } = [];

    public List<Guid> GroupLabelRequests { get; } = [];

    public Task<PackagingRawData?> GetPackagingSheetAsync(
        Guid monthlyDistributionId, PackagingMode mode, int week, CancellationToken cancellationToken = default)
    {
        PackagingSheetRequests.Add((monthlyDistributionId, mode, week));
        return Task.FromResult(PackagingSheets.GetValueOrDefault((monthlyDistributionId, mode, week)));
    }

    public Task UpdatePackagingAsync(
        IReadOnlyList<PackagingProgressUpdate> updates, IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken = default)
    {
        PackagingUpdates.Add((updates, emails));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DistributionPostedEmailEntity>> GetUnsentDistributionPostedEmailsAsync(CancellationToken cancellationToken = default)
    {
        UnsentPostedEmailRequests++;
        return Task.FromResult<IReadOnlyList<DistributionPostedEmailEntity>>([.. UnsentPostedEmails]);
    }

    public Task MarkDistributionPostedEmailSentAsync(Guid distributionPostedEmailId, DateTime dateSent, CancellationToken cancellationToken = default)
    {
        MarkedSentEmails.Add((distributionPostedEmailId, dateSent));
        return Task.CompletedTask;
    }

    public Task<PackagingLabelAddressEntity?> GetLabelParticipantAddressAsync(
        Guid participantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        ParticipantLabelRequests.Add((participantId, monthlyDistributionSchemeId));
        return Task.FromResult(ParticipantLabelAddresses.GetValueOrDefault((participantId, monthlyDistributionSchemeId)));
    }

    public Task<PackagingLabelAddressEntity?> GetLabelGroupAddressAsync(Guid groupAddressId, CancellationToken cancellationToken = default)
    {
        GroupLabelRequests.Add(groupAddressId);
        return Task.FromResult(GroupLabelAddresses.GetValueOrDefault(groupAddressId));
    }
}
