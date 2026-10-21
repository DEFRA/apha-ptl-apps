using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.CategoryItemType;
using PTL.Api.Tests.Lookup;
using PTL.Contracts.CategoryItemType;
using PTL.Core.CategoryItemType;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.Endpoints;

public class CategoryItemTypeControllerTests
{
    private const int CurrentYearId = 2026;

    private static CategoryItemTypeController CreateController(FakeCategoryItemTypeRepository repository, FakeLookupRepository? lookupRepository = null)
    {
        lookupRepository ??= new FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = CurrentYearId + 1, FinalYearId = 9999 },
            Years = [new YearEntity { YearId = CurrentYearId, Year = "2026" }]
        };

        var controller = new CategoryItemTypeController(new CategoryItemTypeService(repository, lookupRepository));

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
        var repository = new FakeCategoryItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = Guid.NewGuid(), Name = "Colour", StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.GetByYear(CurrentYearId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<CategoryItemTypeResponse>>(ok.Value);
        Assert.Equal("Colour", Assert.Single(items).Name);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        var controller = CreateController(new FakeCategoryItemTypeRepository());

        var result = await controller.Create(new CategoryItemTypeSaveRequest("Colour", false, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CategoryItemTypeResponse>(ok.Value);
        Assert.Equal("Colour", response.Name);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeCategoryItemTypeRepository());

        var result = await controller.Create(new CategoryItemTypeSaveRequest(string.Empty, false, CurrentYearId), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCategoryItemTypeRepository());

        var result = await controller.Update(Guid.NewGuid(), new CategoryItemTypeSaveRequest("Colour", false, CurrentYearId), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ItemInUse_ReturnsConflict()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = CurrentYearId, EndYearId = 9999, ForeignInstances = 1, ForeignInstancesString = "Scheme A" }];
        var controller = CreateController(repository);

        var result = await controller.Update(itemId, new CategoryItemTypeSaveRequest("Colour2", false, CurrentYearId), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var saveResult = Assert.IsType<CategoryItemTypeSaveResult>(conflict.Value);
        Assert.False(saveResult.Success);
    }

    [Fact]
    public async Task SetNoLongerInUse_UpdatesFlag()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.SetNoLongerInUse(itemId, new CategoryItemTypeNoLongerInUseRequest(true, CurrentYearId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CategoryItemTypeResponse>(ok.Value);
        Assert.True(response.NoLongerInUse);
    }

    [Fact]
    public async Task Delete_ReturnsResultFromService()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCategoryItemTypeRepository();
        repository.ItemsByYear[CurrentYearId] = [new PTL.Core.CategoryItemType.CategoryItemType { CategoryItemTypeId = itemId, Name = "Colour", StartYearId = CurrentYearId, EndYearId = 9999 }];
        var controller = CreateController(repository);

        var result = await controller.Delete(itemId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CategoryItemTypeDeleteResponse>(ok.Value);
        Assert.True(response.Success);
    }
}
