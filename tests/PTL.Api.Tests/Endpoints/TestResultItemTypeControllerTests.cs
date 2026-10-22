using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.TestResultItemType;
using PTL.Contracts.TestResultItemType;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.Endpoints;

public class TestResultItemTypeControllerTests
{
    private const int CurrentYearId = 2026;
    private static readonly Guid ReturnTypeId = Guid.NewGuid();

    private static TestResultItemTypeController CreateController(FakeTestResultItemTypeRepository repository, FakeLookupRepository? lookupRepository = null)
    {
        lookupRepository ??= new FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = CurrentYearId + 1, FinalYearId = 9999 },
            Years = [new YearEntity { YearId = CurrentYearId, Year = "2026" }],
            TestReturnValueTypes = [new TestReturnValueTypeEntity { TestReturnValueTypeId = ReturnTypeId, Name = "Numeric" }]
        };

        var controller = new TestResultItemTypeController(new PTL.Core.TestResultItemType.TestResultItemTypeService(repository, lookupRepository));

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task GetByYear_ReturnsMappedItems()
    {
        var itemId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var repository = new FakeTestResultItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = itemId, SharedId = sharedId, Name = "Weight", TestReturnValueTypeId = ReturnTypeId, TestReturnValueTypeName = "Numeric", StartYearId = CurrentYearId, EndYearId = 9999, ForeignInstances = 2, ForeignInstancesString = "Scheme A, Scheme B" }];
        var controller = CreateController(repository);

        var result = await controller.GetByYear(CurrentYearId, search: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TestResultItemTypeResponse>>(ok.Value));
        Assert.Equal(itemId, response.TestResultItemTypeId);
        Assert.Equal(sharedId, response.SharedId);
        Assert.Equal("Weight", response.Name);
        Assert.Equal(ReturnTypeId, response.TestReturnValueTypeId);
        Assert.Equal("Numeric", response.TestReturnValueTypeName);
        Assert.Equal(CurrentYearId, response.StartYearId);
        Assert.Equal(9999, response.EndYearId);
        Assert.False(response.NoLongerInUse);
        Assert.Equal(2, response.ForeignInstances);
        Assert.Equal("Scheme A, Scheme B", response.ForeignInstancesString);
    }

    [Fact]
    public async Task GetByYear_WithSearch_FiltersResults()
    {
        var repository = new FakeTestResultItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = Guid.NewGuid(), Name = "Weight", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 },
            new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = Guid.NewGuid(), Name = "Length", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 }
        ];
        var controller = CreateController(repository);

        var result = await controller.GetByYear(CurrentYearId, search: "weig", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<TestResultItemTypeResponse>>(ok.Value);
        Assert.Equal("Weight", Assert.Single(items).Name);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        var controller = CreateController(new FakeTestResultItemTypeRepository());

        var result = await controller.Create(new TestResultItemTypeSaveRequest("Weight", ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestResultItemTypeResponse>(ok.Value);
        Assert.Equal("Weight", response.Name);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeTestResultItemTypeRepository());

        var result = await controller.Create(new TestResultItemTypeSaveRequest(string.Empty, ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_MissingReturnType_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeTestResultItemTypeRepository());

        var result = await controller.Create(new TestResultItemTypeSaveRequest("Weight", Guid.Empty, false, CurrentYearId), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        var controller = CreateController(new FakeTestResultItemTypeRepository());

        var result = await controller.Update(Guid.NewGuid(), new TestResultItemTypeSaveRequest("Weight", ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsOk()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestResultItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Weight", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.Update(itemId, new TestResultItemTypeSaveRequest("Weight2", ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestResultItemTypeResponse>(ok.Value);
        Assert.Equal("Weight2", response.Name);
    }

    [Fact]
    public async Task Update_ItemInUse_ReturnsConflict()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestResultItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Weight", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999, ForeignInstances = 1, ForeignInstancesString = "Scheme A" }];
        var controller = CreateController(repository);

        var result = await controller.Update(itemId, new TestResultItemTypeSaveRequest("Weight2", ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var saveResult = Assert.IsType<TestResultItemTypeSaveResult>(conflict.Value);
        Assert.False(saveResult.Success);
    }

    [Fact]
    public async Task SetNoLongerInUse_UpdatesFlag()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestResultItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Weight", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.SetNoLongerInUse(itemId, new TestResultItemTypeNoLongerInUseRequest(true, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestResultItemTypeResponse>(ok.Value);
        Assert.True(response.NoLongerInUse);
    }

    [Fact]
    public async Task Delete_ReturnsResultFromService()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestResultItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Weight", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.Delete(itemId, CurrentYearId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestResultItemTypeDeleteResponse>(ok.Value);
        Assert.True(response.Success);
    }
}
