using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.CategoryItemType;
using PTL.Contracts.Lookup;
using PTL.Contracts.TestType;
using PTL.InternalWeb.Features.SchemeAdmin;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.SchemeAdmin;

public class SchemeAdminControllerTests
{
    private const int CurrentYearId = 2026;

    private static SchemeAdminController CreateController(FakeCategoryItemTypeApiClient? categoryItemTypeApiClient = null, FakeLookupApiClient? lookupApiClient = null, FakeTestTypeApiClient? testTypeApiClient = null) =>
        new(
            categoryItemTypeApiClient ?? new FakeCategoryItemTypeApiClient(),
            testTypeApiClient ?? new FakeTestTypeApiClient(),
            lookupApiClient ?? new FakeLookupApiClient { Years = [new YearResponse(CurrentYearId, "2026"), new YearResponse(CurrentYearId + 1, "2027")] },
            NullLogger<SchemeAdminController>.Instance);

    [Fact]
    public async Task CategoryItemType_Get_BuildsRowsAndYearOptions()
    {
        var apiClient = new FakeCategoryItemTypeApiClient
        {
            Items = [new CategoryItemTypeResponse(Guid.NewGuid(), "Colour", CurrentYearId, 9999, false, 0, string.Empty)]
        };
        var controller = CreateController(apiClient);

        var result = await controller.CategoryItemType(null, null, null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.Equal(CurrentYearId, model.SelectedYearId);
        Assert.Equal("Colour", Assert.Single(model.Rows).Name);
    }

    [Fact]
    public async Task CategoryItemType_Get_WithEditId_MarksMatchingRowAsEditing()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient
        {
            Items = [new CategoryItemTypeResponse(itemId, "Colour", CurrentYearId, 9999, false, 0, string.Empty)]
        };
        var controller = CreateController(apiClient);

        var result = await controller.CategoryItemType(CurrentYearId, itemId, null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.True(Assert.Single(model.Rows).IsEditing);
    }

    [Fact]
    public async Task CategoryItemType_Get_WithEditIdOnInUseItem_RefusesEditModeAndShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient
        {
            Items = [new CategoryItemTypeResponse(itemId, "Colour", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")]
        };
        var controller = CreateController(apiClient);

        var result = await controller.CategoryItemType(CurrentYearId, itemId, null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.False(Assert.Single(model.Rows).IsEditing);
        Assert.True(model.MessageIsError);
        Assert.Contains("Scheme A, Scheme B", model.Message);
    }

    [Fact]
    public async Task CategoryItemTypeAdd_ValidModel_CreatesAndRedirects()
    {
        var apiClient = new FakeCategoryItemTypeApiClient();
        var controller = CreateController(apiClient);
        var model = new CategoryItemTypeAddViewModel { Name = "Colour", NoLongerInUse = false };

        var result = await controller.CategoryItemTypeAdd(model, CurrentYearId, 1, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.CategoryItemType), redirect.ActionName);
        Assert.Equal("Colour", apiClient.LastCreateRequest?.Name);
    }

    [Fact]
    public async Task CategoryItemTypeAdd_ApiRejectsMissingName_ReturnsViewWithPrefixedError()
    {
        var apiClient = new FakeCategoryItemTypeApiClient
        {
            SaveResult = new CategoryItemTypeSaveResult(false, null, new Dictionary<string, string[]> { ["Name"] = ["Name required"] })
        };
        var controller = CreateController(apiClient);
        var model = new CategoryItemTypeAddViewModel { Name = "Colour" };

        var result = await controller.CategoryItemTypeAdd(model, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.True(controller.ModelState.ContainsKey("Add.Name"));
    }

    [Fact]
    public async Task CategoryItemTypeSave_ValidModel_UpdatesAndRedirects()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient();
        var controller = CreateController(apiClient);
        var model = new CategoryItemTypeEditViewModel { CategoryItemTypeId = itemId, Name = "Colour (renamed)", NoLongerInUse = true };

        var result = await controller.CategoryItemTypeSave(model, CurrentYearId, 1, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.CategoryItemType), redirect.ActionName);
        Assert.Equal(itemId, apiClient.LastUpdateRequest?.CategoryItemTypeId);
    }

    [Fact]
    public async Task CategoryItemTypeSave_ApiReportsInUseConflict_RedisplaysWithBannerMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient
        {
            SaveResult = new CategoryItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This Category Item is being used on 1 Scheme(s): Scheme A and cannot be edited."] })
        };
        var controller = CreateController(apiClient);
        var model = new CategoryItemTypeEditViewModel { CategoryItemTypeId = itemId, Name = "Colour" };

        var result = await controller.CategoryItemTypeSave(model, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.True(resultModel.MessageIsError);
        Assert.Contains("Scheme A", resultModel.Message);
    }

    [Fact]
    public async Task CategoryItemTypeSaveAllNoLongerInUse_TickedAndUntickedRows_SavesBothCorrectly()
    {
        var tickedId = Guid.NewGuid();
        var untickedId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient();
        var controller = CreateController(apiClient);

        // Matches what the view posts: every row's id unconditionally, but only ticked ids.
        var result = await controller.CategoryItemTypeSaveAllNoLongerInUse(
            pageCategoryItemTypeIds: [tickedId, untickedId],
            checkedCategoryItemTypeIds: [tickedId],
            CurrentYearId, 1, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(2, apiClient.AllNoLongerInUseRequests.Count);
        Assert.True(apiClient.AllNoLongerInUseRequests.Single(r => r.CategoryItemTypeId == tickedId).Request.NoLongerInUse);
        Assert.False(apiClient.AllNoLongerInUseRequests.Single(r => r.CategoryItemTypeId == untickedId).Request.NoLongerInUse);
    }

    [Fact]
    public async Task CategoryItemTypeSaveAllNoLongerInUse_NothingTicked_StillSetsAllRowsToFalse()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.CategoryItemTypeSaveAllNoLongerInUse(
            pageCategoryItemTypeIds: [itemId],
            checkedCategoryItemTypeIds: null,
            CurrentYearId, 1, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(apiClient.AllNoLongerInUseRequests.Single().Request.NoLongerInUse);
    }

    [Fact]
    public async Task CategoryItemTypeRemove_Blocked_RedisplaysWithMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient
        {
            DeleteResponse = new CategoryItemTypeDeleteResponse(false, "This Category Item is being used on 1 Scheme(s): Scheme A and cannot be removed.")
        };
        var controller = CreateController(apiClient);

        var result = await controller.CategoryItemTypeRemove(itemId, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public async Task CategoryItemTypeRemove_Success_RedisplaysWithSuccessMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient { DeleteResponse = new CategoryItemTypeDeleteResponse(true, null) };
        var controller = CreateController(apiClient);

        var result = await controller.CategoryItemTypeRemove(itemId, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Equal(itemId, apiClient.LastDeletedCategoryItemTypeId);
    }

    // Matches legacy GridViewCategoryItemType.AllowPaging/PageSize = 15 - 16 items must split
    // across 2 pages, same page size as the legacy grid.
    [Fact]
    public async Task CategoryItemType_Get_WithMoreThan15Items_PaginatesAtLegacyPageSizeOf15()
    {
        var items = Enumerable.Range(1, 16)
            .Select(i => new CategoryItemTypeResponse(Guid.NewGuid(), $"Item {i:D2}", CurrentYearId, 9999, false, 0, string.Empty))
            .ToList();
        var apiClient = new FakeCategoryItemTypeApiClient { Items = items };
        var controller = CreateController(apiClient);

        var page1Result = await controller.CategoryItemType(CurrentYearId, null, null, page: 1, cancellationToken: CancellationToken.None);
        var page1Model = Assert.IsType<CategoryItemTypeViewModel>(Assert.IsType<ViewResult>(page1Result).Model);

        var page2Result = await controller.CategoryItemType(CurrentYearId, null, null, page: 2, cancellationToken: CancellationToken.None);
        var page2Model = Assert.IsType<CategoryItemTypeViewModel>(Assert.IsType<ViewResult>(page2Result).Model);

        Assert.Equal(15, page1Model.Rows.Count);
        Assert.Equal(1, page2Model.Rows.Count);
        Assert.Equal(16, page1Model.TotalRecords);
        Assert.Equal(16, page2Model.TotalRecords);
    }

    [Fact]
    public async Task TestType_Get_BuildsRowsAndYearOptions()
    {
        var apiClient = new FakeTestTypeApiClient
        {
            Items = [new TestTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Chemical", CurrentYearId, 9999, false, 0, string.Empty)]
        };
        var controller = CreateController(testTypeApiClient: apiClient);

        var result = await controller.TestType(null, null, null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestTypeViewModel>(view.Model);
        Assert.Equal(CurrentYearId, model.SelectedYearId);
        Assert.Equal("Chemical", Assert.Single(model.Rows).Name);
    }

    [Fact]
    public async Task TestType_Get_WithEditIdOnInUseItem_RefusesEditModeAndShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestTypeApiClient
        {
            Items = [new TestTypeResponse(itemId, Guid.NewGuid(), "Chemical", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")]
        };
        var controller = CreateController(testTypeApiClient: apiClient);

        var result = await controller.TestType(CurrentYearId, itemId, null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestTypeViewModel>(view.Model);
        Assert.False(Assert.Single(model.Rows).IsEditing);
        Assert.True(model.MessageIsError);
        Assert.Contains("Scheme A, Scheme B", model.Message);
    }

    [Fact]
    public async Task TestTypeAdd_ValidModel_CreatesAndRedirects()
    {
        var apiClient = new FakeTestTypeApiClient();
        var controller = CreateController(testTypeApiClient: apiClient);
        var model = new TestTypeAddViewModel { Name = "Chemical", NoLongerInUse = false };

        var result = await controller.TestTypeAdd(model, CurrentYearId, 1, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.TestType), redirect.ActionName);
        Assert.Equal("Chemical", apiClient.LastCreateRequest?.Name);
    }

    [Fact]
    public async Task TestTypeSave_ValidModel_UpdatesAndRedirects()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestTypeApiClient();
        var controller = CreateController(testTypeApiClient: apiClient);
        var model = new TestTypeEditViewModel { TestTypeId = itemId, Name = "Chemical (renamed)", NoLongerInUse = true };

        var result = await controller.TestTypeSave(model, CurrentYearId, 1, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.TestType), redirect.ActionName);
        Assert.Equal(itemId, apiClient.LastUpdateRequest?.TestTypeId);
    }

    [Fact]
    public async Task TestTypeSave_ApiReportsInUseConflict_RedisplaysWithBannerMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestTypeApiClient
        {
            SaveResult = new TestTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This Test is being used on 1 Scheme(s): Scheme A and cannot be edited."] })
        };
        var controller = CreateController(testTypeApiClient: apiClient);
        var model = new TestTypeEditViewModel { TestTypeId = itemId, Name = "Chemical" };

        var result = await controller.TestTypeSave(model, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<TestTypeViewModel>(view.Model);
        Assert.True(resultModel.MessageIsError);
        Assert.Contains("Scheme A", resultModel.Message);
    }

    [Fact]
    public async Task TestTypeSaveAllNoLongerInUse_TickedAndUntickedRows_SavesBothCorrectly()
    {
        var tickedId = Guid.NewGuid();
        var untickedId = Guid.NewGuid();
        var apiClient = new FakeTestTypeApiClient();
        var controller = CreateController(testTypeApiClient: apiClient);

        var result = await controller.TestTypeSaveAllNoLongerInUse(
            pageTestTypeIds: [tickedId, untickedId],
            checkedTestTypeIds: [tickedId],
            CurrentYearId, 1, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(2, apiClient.AllNoLongerInUseRequests.Count);
        Assert.True(apiClient.AllNoLongerInUseRequests.Single(r => r.TestTypeId == tickedId).Request.NoLongerInUse);
        Assert.False(apiClient.AllNoLongerInUseRequests.Single(r => r.TestTypeId == untickedId).Request.NoLongerInUse);
    }

    [Fact]
    public async Task TestTypeRemove_Blocked_RedisplaysWithMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestTypeApiClient
        {
            DeleteResponse = new TestTypeDeleteResponse(false, "This Test is being used on 1 Scheme(s): Scheme A and cannot be removed.")
        };
        var controller = CreateController(testTypeApiClient: apiClient);

        var result = await controller.TestTypeRemove(itemId, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestTypeViewModel>(view.Model);
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public async Task TestTypeRemove_Success_RedisplaysWithSuccessMessageAndPassesYearIdToDelete()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestTypeApiClient { DeleteResponse = new TestTypeDeleteResponse(true, null) };
        var controller = CreateController(testTypeApiClient: apiClient);

        var result = await controller.TestTypeRemove(itemId, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestTypeViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Equal(itemId, apiClient.LastDeletedTestTypeId);
        Assert.Equal(CurrentYearId, apiClient.LastDeletedYearId);
    }

    // Matches legacy GridViewTestType.AllowPaging/PageSize = 15.
    [Fact]
    public async Task TestType_Get_WithMoreThan15Items_PaginatesAtLegacyPageSizeOf15()
    {
        var items = Enumerable.Range(1, 16)
            .Select(i => new TestTypeResponse(Guid.NewGuid(), Guid.NewGuid(), $"Item {i:D2}", CurrentYearId, 9999, false, 0, string.Empty))
            .ToList();
        var apiClient = new FakeTestTypeApiClient { Items = items };
        var controller = CreateController(testTypeApiClient: apiClient);

        var page1Result = await controller.TestType(CurrentYearId, null, null, page: 1, cancellationToken: CancellationToken.None);
        var page1Model = Assert.IsType<TestTypeViewModel>(Assert.IsType<ViewResult>(page1Result).Model);

        var page2Result = await controller.TestType(CurrentYearId, null, null, page: 2, cancellationToken: CancellationToken.None);
        var page2Model = Assert.IsType<TestTypeViewModel>(Assert.IsType<ViewResult>(page2Result).Model);

        Assert.Equal(15, page1Model.Rows.Count);
        Assert.Equal(1, page2Model.Rows.Count);
        Assert.Equal(16, page1Model.TotalRecords);
        Assert.Equal(16, page2Model.TotalRecords);
    }
}
