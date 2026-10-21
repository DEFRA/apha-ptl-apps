using PTL.Core.Lookup;
using PTL.Core.TestMethodItemType;

namespace PTL.Api.Tests.TestMethodItemType;

public class TestMethodItemTypeServiceTests
{
    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;
    private const int FinalYearId = 9999;

    private static readonly Guid ChemicalReturnTypeId = Guid.NewGuid();
    private static readonly Guid NumericReturnTypeId = Guid.NewGuid();

    private static TestMethodItemTypeService CreateService(FakeTestMethodItemTypeRepository repository, PTL.Api.Tests.Lookup.FakeLookupRepository? lookupRepository = null)
    {
        lookupRepository ??= new PTL.Api.Tests.Lookup.FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = NextYearId, FinalYearId = FinalYearId },
            Years = [new YearEntity { YearId = CurrentYearId, Year = "2026" }, new YearEntity { YearId = NextYearId, Year = "2027" }],
            TestReturnValueTypes =
            [
                new TestReturnValueTypeEntity { TestReturnValueTypeId = ChemicalReturnTypeId, Name = "Chemical" },
                new TestReturnValueTypeEntity { TestReturnValueTypeId = NumericReturnTypeId, Name = "Numeric" }
            ]
        };

        return new TestMethodItemTypeService(repository, lookupRepository);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsWithFinalYearIdAndResolvedReturnTypeName()
    {
        var repository = new FakeTestMethodItemTypeRepository();
        var service = CreateService(repository);

        var created = await service.CreateAsync("Titration", ChemicalReturnTypeId, noLongerInUse: false, CurrentYearId);

        Assert.Equal("Titration", created.Name);
        Assert.Equal(ChemicalReturnTypeId, created.TestReturnValueTypeId);
        Assert.Equal("Chemical", created.TestReturnValueTypeName);
        Assert.Equal(CurrentYearId, created.StartYearId);
        Assert.Equal(FinalYearId, created.EndYearId);
        Assert.NotEqual(Guid.Empty, created.SharedId);
        Assert.Single(repository.Created);
    }

    [Fact]
    public async Task CreateAsync_MissingName_ThrowsValidationException()
    {
        var service = CreateService(new FakeTestMethodItemTypeRepository());

        var ex = await Assert.ThrowsAsync<TestMethodItemTypeValidationException>(() => service.CreateAsync(" ", ChemicalReturnTypeId, false, CurrentYearId));

        Assert.Contains(ex.Errors, e => e.Field == "Name");
    }

    [Fact]
    public async Task CreateAsync_NameOverMaxLength_ThrowsValidationException()
    {
        var service = CreateService(new FakeTestMethodItemTypeRepository());

        var ex = await Assert.ThrowsAsync<TestMethodItemTypeValidationException>(() => service.CreateAsync(new string('a', 101), ChemicalReturnTypeId, false, CurrentYearId));

        Assert.Contains(ex.Errors, e => e.Field == "Name");
    }

    [Fact]
    public async Task CreateAsync_MissingReturnType_ThrowsValidationException()
    {
        var service = CreateService(new FakeTestMethodItemTypeRepository());

        var ex = await Assert.ThrowsAsync<TestMethodItemTypeValidationException>(() => service.CreateAsync("Titration", Guid.Empty, false, CurrentYearId));

        Assert.Contains(ex.Errors, e => e.Field == "TestReturnValueTypeId");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var service = CreateService(new FakeTestMethodItemTypeRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), "New Name", ChemicalReturnTypeId, false, CurrentYearId);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ItemInUse_ThrowsInUseException()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = 2, ForeignInstancesString = "Scheme A, Scheme B" }
        ];
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<TestMethodItemTypeInUseException>(() => service.UpdateAsync(itemId, "Titration2", ChemicalReturnTypeId, false, CurrentYearId));

        Assert.Contains("Scheme A, Scheme B", ex.Message);
    }

    // Matches legacy's 3-way change-detection guard - GridViewTestMethodItemType_RowUpdating only
    // splits/writes "If Not item.Name = newName Or Not item.ReturnValueTypeId = newReturnValueId
    // Or Not item.NoLongerInUse = newNoLongerInUse".
    [Fact]
    public async Task UpdateAsync_NothingChanged_ReturnsExistingUnmodifiedWithNoWrite()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        var original = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId, NoLongerInUse = false };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Titration", ChemicalReturnTypeId, false, CurrentYearId);

        Assert.Same(original, result);
        Assert.Empty(repository.Created);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_OnlyReturnTypeChanged_StillTriggersSplit()
    {
        var itemId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        var original = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = sharedId, Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId, NoLongerInUse = false };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Titration", NumericReturnTypeId, false, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal(NumericReturnTypeId, result!.TestReturnValueTypeId);
        Assert.Equal("Numeric", result.TestReturnValueTypeName);
        Assert.Single(repository.Created);
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_StartsInSelectedCurrentYear_SplitsIntoThisYearRowAndPushesOriginalForwardCarryingSharedIdAndOriginalReturnType()
    {
        var itemId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        var original = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = sharedId, Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, TestReturnValueTypeName = "Chemical", StartYearId = CurrentYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Titration (renamed)", NumericReturnTypeId, true, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal("Titration (renamed)", result!.Name);
        Assert.Equal(NumericReturnTypeId, result.TestReturnValueTypeId);
        Assert.Equal(sharedId, result.SharedId);
        Assert.Equal(CurrentYearId, result.StartYearId);
        Assert.Equal(CurrentYearId, result.EndYearId);
        Assert.NotEqual(itemId, result.TestMethodItemTypeId);

        Assert.Single(repository.Created);
        Assert.Single(repository.Updated);
        Assert.Equal(CurrentYearId + 1, original.StartYearId);
        Assert.Equal(sharedId, original.SharedId);
    }

    [Fact]
    public async Task UpdateAsync_StartsInSelectedNextYear_EditsInPlace()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        var original = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = NextYearId, EndYearId = FinalYearId };
        repository.ItemsByYear[NextYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Titration (renamed)", NumericReturnTypeId, true, NextYearId);

        Assert.Same(original, result);
        Assert.Equal("Titration (renamed)", original.Name);
        Assert.Equal(NumericReturnTypeId, original.TestReturnValueTypeId);
        Assert.True(original.NoLongerInUse);
        Assert.Empty(repository.Created);
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_StartedEarlierYear_EditingCurrentYear_SplitsIntoThisYearAndFutureRowsCarryingSharedIdAndOriginalReturnTypeOnFutureRow()
    {
        var itemId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        var original = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = sharedId, Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, TestReturnValueTypeName = "Chemical", StartYearId = 2020, EndYearId = FinalYearId, NoLongerInUse = false };
        repository.ItemsByYear[CurrentYearId] = [original];
        var service = CreateService(repository);

        var result = await service.UpdateAsync(itemId, "Titration (renamed)", NumericReturnTypeId, true, CurrentYearId);

        Assert.NotNull(result);
        Assert.Equal("Titration (renamed)", result!.Name);
        Assert.Equal(NumericReturnTypeId, result.TestReturnValueTypeId);
        Assert.Equal(CurrentYearId, result.StartYearId);
        Assert.Equal(CurrentYearId, result.EndYearId);

        Assert.Equal(2, repository.Created.Count);
        var future = repository.Created.Single(i => i.TestMethodItemTypeId != result.TestMethodItemTypeId);
        Assert.Equal("Titration", future.Name);
        Assert.Equal(ChemicalReturnTypeId, future.TestReturnValueTypeId);
        Assert.Equal(CurrentYearId + 1, future.StartYearId);
        Assert.Equal(FinalYearId, future.EndYearId);
        Assert.All(repository.Created, i => Assert.Equal(sharedId, i.SharedId));

        Assert.Equal(CurrentYearId - 1, original.EndYearId);
    }

    [Fact]
    public async Task SetNoLongerInUseAsync_ItemInUse_StillAllowsToggle()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        var original = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = 3, ForeignInstancesString = "Scheme A" };
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
        var service = CreateService(new FakeTestMethodItemTypeRepository());

        var result = await service.SetNoLongerInUseAsync(Guid.NewGuid(), true, CurrentYearId);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ItemStartedInSelectedYear_HardDeletes()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        var original = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId };
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
        var repository = new FakeTestMethodItemTypeRepository();
        var original = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = 2020, EndYearId = FinalYearId };
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
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId, ForeignInstances = -1, ForeignInstancesString = "Scheme A" }
        ];
        var service = CreateService(repository);

        var result = await service.DeleteAsync(itemId, CurrentYearId);

        Assert.False(result.Success);
        Assert.Contains("Scheme A", result.Message);
    }

    // AC: "Given a year is selected and the search field is left blank, When Search is selected,
    // Then all Test Method Items for that year are displayed."
    [Fact]
    public async Task GetByYearAsync_BlankSearch_ReturnsAllItemsForYear()
    {
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId },
            new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = Guid.NewGuid(), Name = "Gravimetric", TestReturnValueTypeId = NumericReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId }
        ];
        var service = CreateService(repository);

        var result = await service.GetByYearAsync(CurrentYearId, searchText: null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByYearAsync_WithSearch_ReturnsOnlyMatchingItemsCaseInsensitive()
    {
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ChemicalReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId },
            new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = Guid.NewGuid(), Name = "Gravimetric", TestReturnValueTypeId = NumericReturnTypeId, StartYearId = CurrentYearId, EndYearId = FinalYearId }
        ];
        var service = CreateService(repository);

        var result = await service.GetByYearAsync(CurrentYearId, searchText: "titr");

        Assert.Single(result);
        Assert.Equal("Titration", result[0].Name);
    }
}
