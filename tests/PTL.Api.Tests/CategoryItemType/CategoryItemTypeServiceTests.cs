using PTL.Core.CategoryItemType;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.CategoryItemType;

public class CategoryItemTypeServiceTests
{
    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;
    private const int FinalYearId = 9999;

    private static CategoryItemTypeService CreateService(FakeCategoryItemTypeRepository repository, PTL.Api.Tests.Lookup.FakeLookupRepository? lookupRepository = null)
    {
        lookupRepository ??= new PTL.Api.Tests.Lookup.FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = NextYearId, FinalYearId = FinalYearId },
            Years = [new YearEntity { YearId = CurrentYearId, Year = "2026" }, new YearEntity { YearId = NextYearId, Year = "2027" }]
        };

        return new CategoryItemTypeService(repository, lookupRepository);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsWithFinalYearIdAsEndYear()
    {
        var repository = new FakeCategoryItemTypeRepository();
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
        var service = CreateService(new FakeCategoryItemTypeRepository());

        var ex = await Assert.ThrowsAsync<CategoryItemTypeValidationException>(() => service.CreateAsync(" ", false, CurrentYearId));

        Assert.Contains(ex.Errors, e => e.Field == "Name");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var service = CreateService(new FakeCategoryItemTypeRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), "New Name", false, CurrentYearId);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ItemInUse_ThrowsInUseException()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = 2, ForeignInstancesString = "Scheme A, Scheme B" }
        ];
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<CategoryItemTypeInUseException>(() => service.UpdateAsync(itemId, "Colour2", false, CurrentYearId));

        Assert.Contains("Scheme A, Scheme B", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_StartsInSelectedCurrentYear_SplitsIntoThisYearRowAndPushesOriginalForward()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        var original = new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = CurrentYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Colour (renamed)", true, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal("Colour (renamed)", result!.Name);
        Assert.Equal(CurrentYearId, result.StartYearId);
        Assert.Equal(CurrentYearId, result.EndYearId);
        Assert.NotEqual(itemId, result.CategoryItemTypeId);

        Assert.Single(repository.Created);
        Assert.Single(repository.Updated);
        Assert.Equal(CurrentYearId + 1, original.StartYearId);
        Assert.Equal("Colour", original.Name); // original pushed forward unchanged
    }

    [Fact]
    public async Task UpdateAsync_StartsInSelectedNextYear_EditsInPlace()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        var original = new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = NextYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[NextYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Colour (renamed)", true, NextYearId);

        Assert.Same(original, result);
        Assert.Equal("Colour (renamed)", original.Name);
        Assert.True(original.NoLongerInUse);
        Assert.Empty(repository.Created);
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_StartedEarlierYear_EditingCurrentYear_SplitsIntoThisYearAndFutureRows()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        var original = new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = 2020, EndYearId = FinalYearId, NoLongerInUse = false };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Colour (renamed)", true, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal("Colour (renamed)", result!.Name);
        Assert.Equal(CurrentYearId, result.StartYearId);
        Assert.Equal(CurrentYearId, result.EndYearId);

        Assert.Equal(2, repository.Created.Count);
        var future = repository.Created.Single(i => i.CategoryItemTypeId != result.CategoryItemTypeId);
        Assert.Equal("Colour", future.Name); // future row carries the ORIGINAL pre-edit values
        Assert.Equal(CurrentYearId + 1, future.StartYearId);
        Assert.Equal(FinalYearId, future.EndYearId);

        Assert.Equal(CurrentYearId - 1, original.EndYearId);
    }

    [Fact]
    public async Task UpdateAsync_StartedEarlierYear_EditingNextYear_EndDatesOriginalAndCreatesNextYearRow()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        var original = new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = 2020, EndYearId = FinalYearId };
        repository.ItemsByYear[NextYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Colour (renamed)", false, NextYearId);

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
        var repository = new FakeCategoryItemTypeRepository();
        var original = new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = 3, ForeignInstancesString = "Scheme A" };
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
        var service = CreateService(new FakeCategoryItemTypeRepository());

        var result = await service.SetNoLongerInUseAsync(Guid.NewGuid(), true, CurrentYearId);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_NotInUse_RemovesUnconditionallyRegardlessOfStartYear()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        var original = new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = 2020, EndYearId = FinalYearId };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.DeleteAsync(itemId);

        Assert.True(result.Success);
        Assert.Contains(itemId, repository.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_InUse_BlocksAndReturnsLegacyStyleMessage()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = -1, ForeignInstancesString = "Scheme A" }
        ];
        var service = CreateService(repository);

        var result = await service.DeleteAsync(itemId);

        Assert.False(result.Success);
        Assert.Contains("distribution", result.Message);
        Assert.Empty(repository.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFailure()
    {
        var service = CreateService(new FakeCategoryItemTypeRepository());

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(result.Success);
    }
}
