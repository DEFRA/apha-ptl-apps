using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.TestMethodItemType;
using PTL.Contracts.TestMethodItemType;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.Endpoints;

public class TestMethodItemTypeControllerTests
{
    private const int CurrentYearId = 2026;
    private static readonly Guid ReturnTypeId = Guid.NewGuid();

    private static TestMethodItemTypeController CreateController(FakeTestMethodItemTypeRepository repository, FakeLookupRepository? lookupRepository = null)
    {
        lookupRepository ??= new FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = CurrentYearId + 1, FinalYearId = 9999 },
            Years = [new YearEntity { YearId = CurrentYearId, Year = "2026" }],
            TestReturnValueTypes = [new TestReturnValueTypeEntity { TestReturnValueTypeId = ReturnTypeId, Name = "Numeric" }]
        };

        var controller = new TestMethodItemTypeController(new PTL.Core.TestMethodItemType.TestMethodItemTypeService(repository, lookupRepository));

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
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = sharedId, Name = "Titration", TestReturnValueTypeId = ReturnTypeId, TestReturnValueTypeName = "Numeric", StartYearId = CurrentYearId, EndYearId = 9999, ForeignInstances = 3, ForeignInstancesString = "Scheme C" }];
        var controller = CreateController(repository);

        var result = await controller.GetByYear(CurrentYearId, search: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TestMethodItemTypeResponse>>(ok.Value));
        Assert.Equal(itemId, response.TestMethodItemTypeId);
        Assert.Equal(sharedId, response.SharedId);
        Assert.Equal("Titration", response.Name);
        Assert.Equal(ReturnTypeId, response.TestReturnValueTypeId);
        Assert.Equal("Numeric", response.TestReturnValueTypeName);
        Assert.Equal(CurrentYearId, response.StartYearId);
        Assert.Equal(9999, response.EndYearId);
        Assert.False(response.NoLongerInUse);
        Assert.Equal(3, response.ForeignInstances);
        Assert.Equal("Scheme C", response.ForeignInstancesString);
    }

    [Fact]
    public async Task GetByYear_WithSearch_FiltersResults()
    {
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] =
        [
            new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 },
            new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = Guid.NewGuid(), Name = "Gravimetric", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 }
        ];
        var controller = CreateController(repository);

        var result = await controller.GetByYear(CurrentYearId, search: "titr", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<TestMethodItemTypeResponse>>(ok.Value);
        Assert.Equal("Titration", Assert.Single(items).Name);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        var controller = CreateController(new FakeTestMethodItemTypeRepository());

        var result = await controller.Create(new TestMethodItemTypeSaveRequest("Titration", ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestMethodItemTypeResponse>(ok.Value);
        Assert.Equal("Titration", response.Name);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeTestMethodItemTypeRepository());

        var result = await controller.Create(new TestMethodItemTypeSaveRequest(string.Empty, ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_MissingReturnType_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeTestMethodItemTypeRepository());

        var result = await controller.Create(new TestMethodItemTypeSaveRequest("Titration", Guid.Empty, false, CurrentYearId), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        var controller = CreateController(new FakeTestMethodItemTypeRepository());

        var result = await controller.Update(Guid.NewGuid(), new TestMethodItemTypeSaveRequest("Titration", ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsOk()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.Update(itemId, new TestMethodItemTypeSaveRequest("Titration2", ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestMethodItemTypeResponse>(ok.Value);
        Assert.Equal("Titration2", response.Name);
    }

    [Fact]
    public async Task Update_ItemInUse_ReturnsConflict()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999, ForeignInstances = 1, ForeignInstancesString = "Scheme A" }];
        var controller = CreateController(repository);

        var result = await controller.Update(itemId, new TestMethodItemTypeSaveRequest("Titration2", ReturnTypeId, false, CurrentYearId), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var saveResult = Assert.IsType<TestMethodItemTypeSaveResult>(conflict.Value);
        Assert.False(saveResult.Success);
    }

    [Fact]
    public async Task SetNoLongerInUse_UpdatesFlag()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.SetNoLongerInUse(itemId, new TestMethodItemTypeNoLongerInUseRequest(true, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestMethodItemTypeResponse>(ok.Value);
        Assert.True(response.NoLongerInUse);
    }

    [Fact]
    public async Task Delete_ReturnsResultFromService()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestMethodItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = ReturnTypeId, StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.Delete(itemId, CurrentYearId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestMethodItemTypeDeleteResponse>(ok.Value);
        Assert.True(response.Success);
    }
}
