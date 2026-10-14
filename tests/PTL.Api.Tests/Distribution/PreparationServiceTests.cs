using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Pins legacy PreparationSheet.aspx(.vb) read/save behaviour: FetchPreparationSheet for the read,
// SetDiaryEntryStarted/SetPrepStarted/SetPrepCompleted's cascade-and-never-overwrite rule for the
// write (see PreparationService's own remarks).
public class PreparationServiceTests
{
    private static readonly Guid MonthlyDistributionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SchemeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherSchemeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PreparationSchemeEntity Scheme(DateTime? diaryEntry = null, DateTime? prepStart = null, DateTime? prepComplete = null) => new()
    {
        MonthlyDistributionSchemeId = SchemeId,
        SchemeIdentifier = "PT0060",
        SchemeName = "Mastitis bovine",
        DiaryEntry = diaryEntry,
        PrepStartDate = prepStart,
        PrepCompleteDate = prepComplete,
    };

    private static FakeDistributionRepository RepositoryWith(PreparationSchemeEntity scheme)
    {
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (MonthlyDistributionId, [new MonthlyDistributionSchemeEntity { MonthlyDistributionSchemeId = SchemeId }]);
        repository.PreparationSheets[MonthlyDistributionId] = new PreparationSheetData(MonthlyDistributionId, 2026, 4, [scheme]);
        return repository;
    }

    [Fact]
    public async Task GetPreparationAsync_ScheduleInitialised_ReturnsTheSheet()
    {
        var repository = RepositoryWith(Scheme());
        var service = new PreparationService(repository);

        var result = await service.GetPreparationAsync(2026, 4, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(MonthlyDistributionId, result!.MonthlyDistributionId);
        Assert.Equal(2026, result.YearId);
        Assert.Equal(4, result.MonthId);
        Assert.Single(result.Schemes);
    }

    [Fact]
    public async Task GetPreparationAsync_MonthNotInitialised_ReturnsNull()
    {
        var repository = new FakeDistributionRepository();
        var service = new PreparationService(repository);

        var result = await service.GetPreparationAsync(2026, 4, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SavePreparationAsync_NoUpdates_PersistsNothing()
    {
        var repository = RepositoryWith(Scheme());
        var service = new PreparationService(repository);

        var outcome = await service.SavePreparationAsync(2026, 4, [], CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(repository.UpdatedPreparationSchemes);
    }

    [Fact]
    public async Task SavePreparationAsync_DiaryEntryCheckedOnly_SetsOnlyDiaryEntry()
    {
        var repository = RepositoryWith(Scheme());
        var service = new PreparationService(repository);
        var updates = new[] { new PreparationStageUpdate(SchemeId, DiaryEntryChecked: true, PrepStartChecked: false, PrepCompleteChecked: false) };

        var outcome = await service.SavePreparationAsync(2026, 4, updates, CancellationToken.None);

        Assert.True(outcome.Success);
        var saved = Assert.Single(repository.UpdatedPreparationSchemes);
        Assert.NotNull(saved.DiaryEntry);
        Assert.Null(saved.PrepStartDate);
        Assert.Null(saved.PrepCompleteDate);
    }

    [Fact]
    public async Task SavePreparationAsync_PrepStartCheckedOnly_CascadesToSetDiaryEntryToo()
    {
        var repository = RepositoryWith(Scheme());
        var service = new PreparationService(repository);
        var updates = new[] { new PreparationStageUpdate(SchemeId, DiaryEntryChecked: false, PrepStartChecked: true, PrepCompleteChecked: false) };

        var outcome = await service.SavePreparationAsync(2026, 4, updates, CancellationToken.None);

        Assert.True(outcome.Success);
        var saved = Assert.Single(repository.UpdatedPreparationSchemes);
        Assert.NotNull(saved.DiaryEntry);
        Assert.NotNull(saved.PrepStartDate);
        Assert.Null(saved.PrepCompleteDate);
        Assert.Equal(saved.DiaryEntry, saved.PrepStartDate);
    }

    [Fact]
    public async Task SavePreparationAsync_PrepCompleteCheckedOnly_CascadesToSetDiaryEntryAndPrepStart()
    {
        var repository = RepositoryWith(Scheme());
        var service = new PreparationService(repository);
        var updates = new[] { new PreparationStageUpdate(SchemeId, DiaryEntryChecked: false, PrepStartChecked: false, PrepCompleteChecked: true) };

        var outcome = await service.SavePreparationAsync(2026, 4, updates, CancellationToken.None);

        Assert.True(outcome.Success);
        var saved = Assert.Single(repository.UpdatedPreparationSchemes);
        Assert.NotNull(saved.DiaryEntry);
        Assert.NotNull(saved.PrepStartDate);
        Assert.NotNull(saved.PrepCompleteDate);
        Assert.Equal(saved.DiaryEntry, saved.PrepStartDate);
        Assert.Equal(saved.PrepStartDate, saved.PrepCompleteDate);
    }

    // The core cascade-never-overwrites rule: DiaryEntry/PrepStartDate were stamped on an earlier
    // save, so ticking PrepComplete now must leave their original timestamps untouched.
    [Fact]
    public async Task SavePreparationAsync_EarlierStagesAlreadySet_TickingPrepComplete_DoesNotOverwriteThem()
    {
        var originalDiaryEntry = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var originalPrepStart = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        var repository = RepositoryWith(Scheme(diaryEntry: originalDiaryEntry, prepStart: originalPrepStart));
        var service = new PreparationService(repository);
        var updates = new[] { new PreparationStageUpdate(SchemeId, DiaryEntryChecked: true, PrepStartChecked: true, PrepCompleteChecked: true) };

        var outcome = await service.SavePreparationAsync(2026, 4, updates, CancellationToken.None);

        Assert.True(outcome.Success);
        var saved = Assert.Single(repository.UpdatedPreparationSchemes);
        Assert.Equal(originalDiaryEntry, saved.DiaryEntry);
        Assert.Equal(originalPrepStart, saved.PrepStartDate);
        Assert.NotNull(saved.PrepCompleteDate);
    }

    [Fact]
    public async Task SavePreparationAsync_UpdateForUnknownSchemeId_IsSilentlyIgnored()
    {
        var repository = RepositoryWith(Scheme());
        var service = new PreparationService(repository);
        var updates = new[] { new PreparationStageUpdate(OtherSchemeId, DiaryEntryChecked: true, PrepStartChecked: true, PrepCompleteChecked: true) };

        var outcome = await service.SavePreparationAsync(2026, 4, updates, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(repository.UpdatedPreparationSchemes);
    }

    [Fact]
    public async Task SavePreparationAsync_MonthNotInitialised_ReturnsUnsuccessfulOutcome()
    {
        var repository = new FakeDistributionRepository();
        var service = new PreparationService(repository);
        var updates = new[] { new PreparationStageUpdate(SchemeId, true, true, true) };

        var outcome = await service.SavePreparationAsync(2026, 4, updates, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Empty(repository.UpdatedPreparationSchemes);
    }
}
