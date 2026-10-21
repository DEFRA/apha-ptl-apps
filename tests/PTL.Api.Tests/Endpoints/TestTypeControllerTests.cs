using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.TestType;
using PTL.Contracts.TestType;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.Endpoints;

public class TestTypeControllerTests
{
    private const int CurrentYearId = 2026;

    private static TestTypeController CreateController(FakeTestTypeRepository repository, FakeLookupRepository? lookupRepository = null)
    {
        lookupRepository ??= new FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = CurrentYearId + 1, FinalYearId = 9999 },
            Years = [new YearEntity { YearId = CurrentYearId, Year = "2026" }]
        };

        var controller = new TestTypeController(new PTL.Core.TestType.TestTypeService(repository, lookupRepository));

        // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices.
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
        var repository = new FakeTestTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestType.TestType { TestTypeId = Guid.NewGuid(), SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.GetByYear(CurrentYearId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<TestTypeResponse>>(ok.Value);
        Assert.Equal("Chemical", Assert.Single(items).Name);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        var controller = CreateController(new FakeTestTypeRepository());

        var result = await controller.Create(new TestTypeSaveRequest("Chemical", false, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestTypeResponse>(ok.Value);
        Assert.Equal("Chemical", response.Name);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeTestTypeRepository());

        var result = await controller.Create(new TestTypeSaveRequest(string.Empty, false, CurrentYearId), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        var controller = CreateController(new FakeTestTypeRepository());

        var result = await controller.Update(Guid.NewGuid(), new TestTypeSaveRequest("Chemical", false, CurrentYearId), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ItemInUse_ReturnsConflict()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = 9999, ForeignInstances = 1, ForeignInstancesString = "Scheme A" }];
        var controller = CreateController(repository);

        var result = await controller.Update(itemId, new TestTypeSaveRequest("Chemical2", false, CurrentYearId), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var saveResult = Assert.IsType<TestTypeSaveResult>(conflict.Value);
        Assert.False(saveResult.Success);
    }

    [Fact]
    public async Task SetNoLongerInUse_UpdatesFlag()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.SetNoLongerInUse(itemId, new TestTypeNoLongerInUseRequest(true, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestTypeResponse>(ok.Value);
        Assert.True(response.NoLongerInUse);
    }

    [Fact]
    public async Task Delete_ReturnsResultFromService()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeTestTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.TestType.TestType { TestTypeId = itemId, SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.Delete(itemId, CurrentYearId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestTypeDeleteResponse>(ok.Value);
        Assert.True(response.Success);
    }
}
