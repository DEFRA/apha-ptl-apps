using PTL.Core.Scheme;

namespace PTL.Api.Tests.Scheme;

// In-memory ISchemeRepository test double so SchemeService can be tested without a real database
// or the spgScheme*/spiScheme/spuScheme stored procedures.
internal sealed class FakeSchemeRepository : ISchemeRepository
{
    private readonly Dictionary<Guid, PTL.Core.Scheme.Scheme> _schemes = [];

    // Simulates a concurrent delete between UpdateSchemeAsync's existence check and its actual
    // update call - SchemeService must treat a null UpdateAsync result as "not found", not throw.
    public bool ForceUpdateReturnsNull { get; set; }

    public Task<PTL.Core.Scheme.Scheme?> GetByIdAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_schemes.TryGetValue(schemeId, out var scheme) ? Clone(scheme) : null);

    public Task<IReadOnlyList<SchemeSummaryEntity>> GetAllSummariesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SchemeSummaryEntity> summaries = _schemes.Values
            .GroupBy(s => s.SharedId)
            .Select(family =>
            {
                var mostRecent = family.OrderByDescending(s => s.YearId).First();
                return new SchemeSummaryEntity
                {
                    SharedId = family.Key,
                    YearId = mostRecent.YearId,
                    RecentSchemeId = mostRecent.SchemeId,
                    RecentIdentifier = mostRecent.Identifier,
                    RecentName = mostRecent.Name
                };
            })
            .ToList();

        return Task.FromResult(summaries);
    }

    public Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SchemeSummaryEntity> summaries = _schemes.Values
            .Where(s => s.YearId == yearId)
            .Select(s => new SchemeSummaryEntity
            {
                SharedId = s.SharedId,
                YearId = s.YearId,
                CurrentSchemeId = s.SchemeId,
                CurrentIdentifier = s.Identifier,
                CurrentName = s.Name
            })
            .ToList();

        return Task.FromResult(summaries);
    }

    public Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesBySchemeIdAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SchemeSummaryEntity> summaries = _schemes.TryGetValue(schemeId, out var scheme)
            ?
            [
                new SchemeSummaryEntity
                {
                    SharedId = scheme.SharedId,
                    YearId = scheme.YearId,
                    CurrentSchemeId = scheme.SchemeId,
                    CurrentIdentifier = scheme.Identifier,
                    CurrentName = scheme.Name,
                    NextSchemeId = _schemes.Values.FirstOrDefault(s => s.SharedId == scheme.SharedId && s.YearId == scheme.YearId + 1)?.SchemeId
                }
            ]
            : [];

        return Task.FromResult(summaries);
    }

    public Task<IReadOnlyList<SchemeHistoryEntity>> GetHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SchemeHistoryEntity> history = _schemes.Values
            .Where(s => s.SharedId == sharedId)
            .OrderByDescending(s => s.YearId)
            .Select(s => new SchemeHistoryEntity
            {
                SchemeId = s.SchemeId,
                SharedId = s.SharedId,
                YearId = s.YearId,
                Identifier = s.Identifier,
                Name = s.Name
            })
            .ToList();

        return Task.FromResult(history);
    }

    public Task<PTL.Core.Scheme.Scheme> CreateAsync(PTL.Core.Scheme.Scheme scheme, CancellationToken cancellationToken = default)
    {
        scheme.SampleNoSequence = 1;
        scheme.IsReadOnly = scheme.YearId < DateTime.UtcNow.Year;
        _schemes[scheme.SchemeId] = Clone(scheme);
        return Task.FromResult(Clone(scheme));
    }

    public Task<PTL.Core.Scheme.Scheme?> UpdateAsync(PTL.Core.Scheme.Scheme scheme, CancellationToken cancellationToken = default)
    {
        if (ForceUpdateReturnsNull || !_schemes.TryGetValue(scheme.SchemeId, out var existing))
        {
            return Task.FromResult<PTL.Core.Scheme.Scheme?>(null);
        }

        scheme.SampleNoSequence = existing.SampleNoSequence;
        scheme.IsReadOnly = scheme.YearId < DateTime.UtcNow.Year;
        _schemes[scheme.SchemeId] = Clone(scheme);
        return Task.FromResult<PTL.Core.Scheme.Scheme?>(Clone(scheme));
    }

    private static PTL.Core.Scheme.Scheme Clone(PTL.Core.Scheme.Scheme source) => new()
    {
        SchemeId = source.SchemeId,
        SharedId = source.SharedId,
        YearId = source.YearId,
        Identifier = source.Identifier,
        Name = source.Name,
        ScheduleId = source.ScheduleId,
        ScheduleCodeId = source.ScheduleCodeId,
        StartDate = source.StartDate,
        DistributionMonthApr = source.DistributionMonthApr,
        DistributionMonthMay = source.DistributionMonthMay,
        DistributionMonthJun = source.DistributionMonthJun,
        DistributionMonthJul = source.DistributionMonthJul,
        DistributionMonthAug = source.DistributionMonthAug,
        DistributionMonthSep = source.DistributionMonthSep,
        DistributionAsAvailable = source.DistributionAsAvailable,
        DistributionMonthOct = source.DistributionMonthOct,
        DistributionMonthNov = source.DistributionMonthNov,
        DistributionMonthDec = source.DistributionMonthDec,
        DistributionMonthJan = source.DistributionMonthJan,
        DistributionMonthFeb = source.DistributionMonthFeb,
        DistributionMonthMar = source.DistributionMonthMar,
        CanEditApr = source.CanEditApr,
        CanEditMay = source.CanEditMay,
        CanEditJun = source.CanEditJun,
        CanEditJul = source.CanEditJul,
        CanEditAug = source.CanEditAug,
        CanEditSep = source.CanEditSep,
        CanEditOct = source.CanEditOct,
        CanEditNov = source.CanEditNov,
        CanEditDec = source.CanEditDec,
        CanEditJan = source.CanEditJan,
        CanEditFeb = source.CanEditFeb,
        CanEditMar = source.CanEditMar,
        WeekNumber = source.WeekNumber,
        DayOfWeekId = source.DayOfWeekId,
        NumberOfSamples = source.NumberOfSamples,
        SampleNoSequence = source.SampleNoSequence,
        SampleOrigin = source.SampleOrigin,
        Deadline = source.Deadline,
        Subcontractor = source.Subcontractor,
        CombinedPackaging = source.CombinedPackaging,
        Postage = source.Postage,
        CustomsVolume = source.CustomsVolume,
        SamplePackingInstructions = source.SamplePackingInstructions,
        RequiresAssessment = source.RequiresAssessment,
        CommentsRequired = source.CommentsRequired,
        Pilot = source.Pilot,
        LimitedSampleAvailability = source.LimitedSampleAvailability,
        Accredited = source.Accredited,
        NoVLALabs = source.NoVLALabs,
        ComerciallyAvailable = source.ComerciallyAvailable,
        CustomsDescription = source.CustomsDescription,
        DataConsentDeclarationActive = source.DataConsentDeclarationActive,
        DataConsentDeclarationText = source.DataConsentDeclarationText,
        Instructions = source.Instructions,
        DateOfReceipt = source.DateOfReceipt,
        StorageConditions = source.StorageConditions,
        ConditionOnReceipt = source.ConditionOnReceipt,
        TestConsultant1 = source.TestConsultant1,
        TestConsultant2 = source.TestConsultant2,
        TestConsultant3 = source.TestConsultant3,
        TestConsultantTabulationId = source.TestConsultantTabulationId,
        UseExternalReference = source.UseExternalReference,
        StoreRatings = source.StoreRatings,
        Assessor1 = source.Assessor1,
        Assessor2 = source.Assessor2,
        Assessor3 = source.Assessor3,
        Assessor4 = source.Assessor4,
        StandardTabulationText = source.StandardTabulationText,
        LastModified = source.LastModified,
        IsReadOnly = source.IsReadOnly,
        Prices = [.. source.Prices],
        ViewerIds = [.. source.ViewerIds],
        Tests = [.. source.Tests],
        Tabulations = [.. source.Tabulations]
    };
}
