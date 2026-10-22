using PTL.Core.Lookup;
using PTL.Core.TestType;

namespace PTL.Api.Tests.TestType;

public class TestTypeServiceTests
{
    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;
    private const int FinalYearId = 9999;

    private static TestTypeService CreateService(FakeTestTypeRepository repository, PTL.Api.Tests.Lookup.FakeLookupRepository? lookupRepository = null)
    {
        lookupRepository ??= new PTL.Api.Tests.Lookup.FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = NextYearId, FinalYearId = FinalYearId },
            Years = [new YearEntity { YearId = CurrentYearId, Year = "2026" }, new YearEntity { YearId = NextYearId, Year = "2027" }]
        };

        return new TestTypeService(repository, lookupRepository);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsWithFinalYearIdAsEndYearAndNewSharedId()
    {
        var repository = new FakeTestTypeRepository();
        var service = CreateService(repository);

        var created = await service.CreateAsync("Chemical", noLongerInUse: false, CurrentYearId);

        Assert.Equal("Chemical", created.Name);
        Assert.Equal(CurrentYearId, created.StartYearId);
        Assert.Equal(FinalYearId, created.EndYearId);
        Assert.NotEqual(Guid.Empty, created.SharedId);
        Assert.Single(repository.Created);
    }

    [Fact]
    public async Task CreateAsync_MissingName_ThrowsValidationException()
    {
        var service = CreateService(new FakeTestTypeRepository());

        var ex = await Assert.ThrowsAsync<TestTypeValidationException>(() => service.CreateAsync(" ", false, CurrentYearId));

        Assert.Contains(ex.Errors, e => e.Field == "Name");
    }

    [Fact]
    public async Task CreateAsync_NameOverMaxLength_ThrowsValidationException()
    {
        var service = CreateService(new FakeTestTypeRepository());

        var ex = await Assert.ThrowsAsync<TestTypeValidationException>(() => service.CreateAsync(new string('a', 31), false, CurrentYearId));

        Assert.Contains(ex.Errors, e => e.Field == "Name");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var service = CreateService(new FakeTestTypeRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), "New Name", false, CurrentYearId);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ItemInUse_ThrowsInUseException()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = 2, ForeignInstancesString = "Scheme A, Scheme B" }
        ];
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<TestTypeInUseException>(() => service.UpdateAsync(itemId, "Chemical2", false, CurrentYearId));

        Assert.Contains("Scheme A, Scheme B", ex.Message);
    }

    // Matches legacy's change-detection guard - GridViewTestType_RowUpdating only splits/writes
    // "If Not item.Name = newName Or Not item.NoLongerInUse = newNoLongerInUse" - unlike Category
    // Items, which always re-splits on Save.
    [Fact]
    public async Task UpdateAsync_NothingChanged_ReturnsExistingUnmodifiedWithNoWrite()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        var original = new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = FinalYearId, NoLongerInUse = false };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Chemical", false, CurrentYearId);

        Assert.Same(original, result);
        Assert.Empty(repository.Created);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_StartsInSelectedCurrentYear_SplitsIntoThisYearRowAndPushesOriginalForwardCarryingSharedId()
    {
        var itemId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        var original = new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = sharedId, Name = "Chemical", StartYearId = CurrentYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Chemical (renamed)", true, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal("Chemical (renamed)", result!.Name);
        Assert.Equal(sharedId, result.SharedId);
        Assert.Equal(CurrentYearId, result.StartYearId);
        Assert.Equal(CurrentYearId, result.EndYearId);
        Assert.NotEqual(itemId, result.TestTypeId);

        Assert.Single(repository.Created);
        Assert.Single(repository.Updated);
        Assert.Equal(CurrentYearId + 1, original.StartYearId);
        Assert.Equal(sharedId, original.SharedId);
    }

    [Fact]
    public async Task UpdateAsync_StartsInSelectedNextYear_EditsInPlace()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        var original = new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = NextYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[NextYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Chemical (renamed)", true, NextYearId);

        Assert.Same(original, result);
        Assert.Equal("Chemical (renamed)", original.Name);
        Assert.True(original.NoLongerInUse);
        Assert.Empty(repository.Created);
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_StartedEarlierYear_EditingCurrentYear_SplitsIntoThisYearAndFutureRowsCarryingSharedId()
    {
        var itemId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        var original = new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = sharedId, Name = "Chemical", StartYearId = 2020, EndYearId = FinalYearId, NoLongerInUse = false };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Chemical (renamed)", true, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal("Chemical (renamed)", result!.Name);
        Assert.Equal(CurrentYearId, result.StartYearId);
        Assert.Equal(CurrentYearId, result.EndYearId);

        Assert.Equal(2, repository.Created.Count);
        var future = repository.Created.Single(i => i.TestTypeId != result.TestTypeId);
        Assert.Equal("Chemical", future.Name);
        Assert.Equal(CurrentYearId + 1, future.StartYearId);
        Assert.Equal(FinalYearId, future.EndYearId);
        Assert.All(repository.Created, i => Assert.Equal(sharedId, i.SharedId));

        Assert.Equal(CurrentYearId - 1, original.EndYearId);
    }

    [Fact]
    public async Task UpdateAsync_StartedEarlierYear_EditingNextYear_EndDatesOriginalAndCreatesNextYearRow()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        var original = new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = 2020, EndYearId = FinalYearId };
        repository.ItemsByYear[NextYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Chemical (renamed)", false, NextYearId);

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
        var repository = new FakeTestTypeRepository();
        var original = new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = 3, ForeignInstancesString = "Scheme A" };
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
        var service = CreateService(new FakeTestTypeRepository());

        var result = await service.SetNoLongerInUseAsync(Guid.NewGuid(), true, CurrentYearId);

        Assert.Null(result);
    }

    // Year-scoped delete, unlike Category Items: hard-deletes only when the row started in the
    // currently selected year - matches legacy GridViewTestType_RowDeleting.
    [Fact]
    public async Task DeleteAsync_ItemStartedInSelectedYear_HardDeletes()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        var original = new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = FinalYearId };
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
        var repository = new FakeTestTypeRepository();
        var original = new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = 2020, EndYearId = FinalYearId };
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
        var repository = new FakeTestTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = -1, ForeignInstancesString = "Scheme A" }
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
        var service = CreateService(new FakeTestTypeRepository());

        var result = await service.DeleteAsync(Guid.NewGuid(), CurrentYearId);

        Assert.False(result.Success);
    }
}
