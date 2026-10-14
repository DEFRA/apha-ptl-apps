using PTL.Core.CriterionItemType;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.CriterionItemType;

public class CriterionItemTypeServiceTests
{
    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;
    private const int FinalYearId = 9999;

    private static CriterionItemTypeService CreateService(FakeCriterionItemTypeRepository repository, PTL.Api.Tests.Lookup.FakeLookupRepository? lookupRepository = null)
    {
        lookupRepository ??= new PTL.Api.Tests.Lookup.FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = NextYearId, FinalYearId = FinalYearId },
            Years = [new YearEntity { YearId = CurrentYearId, Year = "2026" }, new YearEntity { YearId = NextYearId, Year = "2027" }]
        };

        return new CriterionItemTypeService(repository, lookupRepository);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsWithFinalYearIdAsEndYear()
    {
        var repository = new FakeCriterionItemTypeRepository();
        var service = CreateService(repository);

        var created = await service.CreateAsync("Appearance", noLongerInUse: false, CurrentYearId);

        Assert.Equal("Appearance", created.Name);
        Assert.Equal(CurrentYearId, created.StartYearId);
        Assert.Equal(FinalYearId, created.EndYearId);
        Assert.Single(repository.Created);
    }

    [Fact]
    public async Task CreateAsync_MissingName_ThrowsValidationException()
    {
        var service = CreateService(new FakeCriterionItemTypeRepository());

        var ex = await Assert.ThrowsAsync<CriterionItemTypeValidationException>(() => service.CreateAsync(" ", false, CurrentYearId));

        Assert.Contains(ex.Errors, e => e.Field == "Name");
    }

    [Fact]
    public async Task CreateAsync_NameOverMaxLength_ThrowsValidationException()
    {
        var service = CreateService(new FakeCriterionItemTypeRepository());

        var ex = await Assert.ThrowsAsync<CriterionItemTypeValidationException>(() => service.CreateAsync(new string('a', 51), false, CurrentYearId));

        Assert.Contains(ex.Errors, e => e.Field == "Name");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var service = CreateService(new FakeCriterionItemTypeRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), "New Name", false, CurrentYearId);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ItemInUse_ThrowsInUseException()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = 2, ForeignInstancesString = "Scheme A, Scheme B" }
        ];
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<CriterionItemTypeInUseException>(() => service.UpdateAsync(itemId, "Appearance2", false, CurrentYearId));

        Assert.Contains("Scheme A, Scheme B", ex.Message);
    }

    // Unlike Test Types, there is no change-detection guard - always re-splits on Save, same as
    // Category Items.
    [Fact]
    public async Task UpdateAsync_NothingChanged_StillSplits()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        var original = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = CurrentYearId, EndYearId = FinalYearId, NoLongerInUse = false };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Appearance", false, CurrentYearId);

        Assert.NotSame(original, result);
        Assert.Single(repository.Created);
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_StartsInSelectedCurrentYear_SplitsIntoThisYearRowAndPushesOriginalForward()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        var original = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = CurrentYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Appearance (renamed)", true, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal("Appearance (renamed)", result!.Name);
        Assert.Equal(CurrentYearId, result.StartYearId);
        Assert.Equal(CurrentYearId, result.EndYearId);
        Assert.NotEqual(itemId, result.CriterionItemTypeId);

        Assert.Single(repository.Created);
        Assert.Single(repository.Updated);
        Assert.Equal(CurrentYearId + 1, original.StartYearId);
        Assert.Equal("Appearance", original.Name);
    }

    [Fact]
    public async Task UpdateAsync_StartsInSelectedNextYear_EditsInPlace()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        var original = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = NextYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[NextYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Appearance (renamed)", true, NextYearId);

        Assert.Same(original, result);
        Assert.Equal("Appearance (renamed)", original.Name);
        Assert.True(original.NoLongerInUse);
        Assert.Empty(repository.Created);
        Assert.Single(repository.Updated);
    }

    // Confirmed genuine legacy quirk, reproduced faithfully: the "future" row carries the
    // ORIGINAL (pre-edit) Name forward, but the NEW (post-edit) NoLongerInUse value - unlike
    // Category/Test Types, where the future row keeps both original values.
    [Fact]
    public async Task UpdateAsync_StartedEarlierYear_EditingCurrentYear_FutureRowKeepsOriginalNameButNewNoLongerInUse()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        var original = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = 2020, EndYearId = FinalYearId, NoLongerInUse = false };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Appearance (renamed)", true, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal("Appearance (renamed)", result!.Name);
        Assert.True(result.NoLongerInUse);

        Assert.Equal(2, repository.Created.Count);
        var future = repository.Created.Single(i => i.CriterionItemTypeId != result.CriterionItemTypeId);
        Assert.Equal("Appearance", future.Name); // original Name
        Assert.True(future.NoLongerInUse); // NEW NoLongerInUse, not the original "false"
        Assert.Equal(CurrentYearId + 1, future.StartYearId);
        Assert.Equal(FinalYearId, future.EndYearId);

        Assert.Equal(CurrentYearId - 1, original.EndYearId);
    }

    [Fact]
    public async Task UpdateAsync_StartedEarlierYear_EditingNextYear_EndDatesOriginalAndCreatesNextYearRow()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        var original = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = 2020, EndYearId = FinalYearId };
        repository.ItemsByYear[NextYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Appearance (renamed)", false, NextYearId);

        Assert.NotNull(result);
        Assert.Equal(NextYearId, result!.StartYearId);
        Assert.Equal(FinalYearId, result.EndYearId);
        Assert.Single(repository.Created);
        Assert.Equal(NextYearId - 1, original.EndYearId);
    }

    [Fact]
    public async Task SetNoLongerInUseAsync_ItemInUse_StillAllowsToggle()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        var original = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = 3, ForeignInstancesString = "Scheme A" };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.SetNoLongerInUseAsync(itemId, true, CurrentYearId);

        Assert.NotNull(result);
        Assert.True(result!.NoLongerInUse);
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task SetNoLongerInUseAsync_NotFound_ReturnsNull()
    {
        var service = CreateService(new FakeCriterionItemTypeRepository());

        var result = await service.SetNoLongerInUseAsync(Guid.NewGuid(), true, CurrentYearId);

        Assert.Null(result);
    }

    // Year-scoped delete, like Test Types: hard-deletes only when the row started in the
    // currently selected year - matches legacy GridViewCriterionItemType_RowDeleting.
    [Fact]
    public async Task DeleteAsync_ItemStartedInSelectedYear_HardDeletes()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        var original = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = CurrentYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.DeleteAsync(itemId, CurrentYearId);

        Assert.True(result.Success);
        Assert.Contains(itemId, repository.Deleted);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task DeleteAsync_ItemStartedEarlierYear_SoftEndsInsteadOfHardDeleting()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        var original = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = 2020, EndYearId = FinalYearId };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.DeleteAsync(itemId, CurrentYearId);

        Assert.True(result.Success);
        Assert.DoesNotContain(itemId, repository.Deleted);
        Assert.Single(repository.Updated);
        Assert.Equal(CurrentYearId - 1, original.EndYearId);
    }

    [Fact]
    public async Task DeleteAsync_InUse_BlocksAndReturnsLegacyStyleMessage()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCriterionItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = itemId, Name = "Appearance", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = -1, ForeignInstancesString = "Scheme A" }
        ];
        var service = CreateService(repository);

        var result = await service.DeleteAsync(itemId, CurrentYearId);

        Assert.False(result.Success);
        Assert.Contains("distribution", result.Message);
        Assert.Empty(repository.Deleted);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFailure()
    {
        var service = CreateService(new FakeCriterionItemTypeRepository());

        var result = await service.DeleteAsync(Guid.NewGuid(), CurrentYearId);

        Assert.False(result.Success);
    }
}
