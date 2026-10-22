using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.CategoryItemType;
using PTL.Contracts.CriterionItemType;
using PTL.Contracts.Lookup;
using PTL.Contracts.TestMethodItemType;
using PTL.Contracts.TestResultItemType;
using PTL.Contracts.TestType;
using PTL.InternalWeb.Features.SchemeAdmin;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.SchemeAdmin;

public class SchemeAdminControllerTests
{
    private const int CurrentYearId = 2026;

    private static SchemeAdminController CreateController(FakeCategoryItemTypeApiClient? categoryItemTypeApiClient = null, FakeLookupApiClient? lookupApiClient = null, FakeTestTypeApiClient? testTypeApiClient = null, FakeCriterionItemTypeApiClient? criterionItemTypeApiClient = null, FakeTestResultItemTypeApiClient? testResultItemTypeApiClient = null, FakeTestMethodItemTypeApiClient? testMethodItemTypeApiClient = null) =>
        new(
            categoryItemTypeApiClient ?? new FakeCategoryItemTypeApiClient(),
            testTypeApiClient ?? new FakeTestTypeApiClient(),
            criterionItemTypeApiClient ?? new FakeCriterionItemTypeApiClient(),
            testResultItemTypeApiClient ?? new FakeTestResultItemTypeApiClient(),
            testMethodItemTypeApiClient ?? new FakeTestMethodItemTypeApiClient(),
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

    // Covers SchemeAdminController.IsTransientApiFailure, shared by all 3 entities' SaveAllNoLongerInUse.
    [Fact]
    public async Task CategoryItemTypeSaveAllNoLongerInUse_ApiThrowsTransientFailure_RedisplaysWithFailureMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCategoryItemTypeApiClient
        {
            Items = [new CategoryItemTypeResponse(itemId, "Colour", CurrentYearId, 9999, false, 0, string.Empty)],
            SetNoLongerInUseException = new HttpRequestException("Connection refused")
        };
        var controller = CreateController(apiClient);

        var result = await controller.CategoryItemTypeSaveAllNoLongerInUse(
            pageCategoryItemTypeIds: [itemId],
            checkedCategoryItemTypeIds: [itemId],
            CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Contains("did not respond", model.Message);
    }

    // Covers ResolveSelectedYearId's fallback when no years exist at all (currentYearId is null
    // and availableYears is empty), distinct from the usual "falls back to the first year" path.
    [Fact]
    public async Task CategoryItemType_Get_NoYearsAvailable_SelectsZeroWithEmptyYearOptions()
    {
        var apiClient = new FakeCategoryItemTypeApiClient();
        var controller = CreateController(apiClient, lookupApiClient: new FakeLookupApiClient { Years = [] });

        var result = await controller.CategoryItemType(null, null, null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CategoryItemTypeViewModel>(view.Model);
        Assert.Equal(0, model.SelectedYearId);
        Assert.Empty(model.YearOptions);
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
        Assert.Single(page2Model.Rows);
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
        Assert.Single(page2Model.Rows);
        Assert.Equal(16, page1Model.TotalRecords);
        Assert.Equal(16, page2Model.TotalRecords);
    }

    [Fact]
    public async Task CriterionItemType_Get_BuildsRowsAndYearOptions()
    {
        var apiClient = new FakeCriterionItemTypeApiClient
        {
            Items = [new CriterionItemTypeResponse(Guid.NewGuid(), "Appearance", CurrentYearId, 9999, false, 0, string.Empty)]
        };
        var controller = CreateController(criterionItemTypeApiClient: apiClient);

        var result = await controller.CriterionItemType(null, null, null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CriterionItemTypeViewModel>(view.Model);
        Assert.Equal(CurrentYearId, model.SelectedYearId);
        Assert.Equal("Appearance", Assert.Single(model.Rows).Name);
    }

    [Fact]
    public async Task CriterionItemType_Get_WithEditIdOnInUseItem_RefusesEditModeAndShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCriterionItemTypeApiClient
        {
            Items = [new CriterionItemTypeResponse(itemId, "Appearance", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")]
        };
        var controller = CreateController(criterionItemTypeApiClient: apiClient);

        var result = await controller.CriterionItemType(CurrentYearId, itemId, null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CriterionItemTypeViewModel>(view.Model);
        Assert.False(Assert.Single(model.Rows).IsEditing);
        Assert.True(model.MessageIsError);
        Assert.Contains("Scheme A, Scheme B", model.Message);
    }

    [Fact]
    public async Task CriterionItemTypeAdd_ValidModel_CreatesAndRedirects()
    {
        var apiClient = new FakeCriterionItemTypeApiClient();
        var controller = CreateController(criterionItemTypeApiClient: apiClient);
        var model = new CriterionItemTypeAddViewModel { Name = "Appearance", NoLongerInUse = false };

        var result = await controller.CriterionItemTypeAdd(model, CurrentYearId, 1, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.CriterionItemType), redirect.ActionName);
        Assert.Equal("Appearance", apiClient.LastCreateRequest?.Name);
    }

    [Fact]
    public async Task CriterionItemTypeSave_ValidModel_UpdatesAndRedirects()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCriterionItemTypeApiClient();
        var controller = CreateController(criterionItemTypeApiClient: apiClient);
        var model = new CriterionItemTypeEditViewModel { CriterionItemTypeId = itemId, Name = "Appearance (renamed)", NoLongerInUse = true };

        var result = await controller.CriterionItemTypeSave(model, CurrentYearId, 1, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.CriterionItemType), redirect.ActionName);
        Assert.Equal(itemId, apiClient.LastUpdateRequest?.CriterionItemTypeId);
    }

    [Fact]
    public async Task CriterionItemTypeSave_ApiReportsInUseConflict_RedisplaysWithBannerMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCriterionItemTypeApiClient
        {
            SaveResult = new CriterionItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This Criterion Item is being used on 1 Scheme(s): Scheme A and cannot be edited."] })
        };
        var controller = CreateController(criterionItemTypeApiClient: apiClient);
        var model = new CriterionItemTypeEditViewModel { CriterionItemTypeId = itemId, Name = "Appearance" };

        var result = await controller.CriterionItemTypeSave(model, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CriterionItemTypeViewModel>(view.Model);
        Assert.True(resultModel.MessageIsError);
        Assert.Contains("Scheme A", resultModel.Message);
    }

    [Fact]
    public async Task CriterionItemTypeSaveAllNoLongerInUse_TickedAndUntickedRows_SavesBothCorrectly()
    {
        var tickedId = Guid.NewGuid();
        var untickedId = Guid.NewGuid();
        var apiClient = new FakeCriterionItemTypeApiClient();
        var controller = CreateController(criterionItemTypeApiClient: apiClient);

        var result = await controller.CriterionItemTypeSaveAllNoLongerInUse(
            pageCriterionItemTypeIds: [tickedId, untickedId],
            checkedCriterionItemTypeIds: [tickedId],
            CurrentYearId, 1, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(2, apiClient.AllNoLongerInUseRequests.Count);
        Assert.True(apiClient.AllNoLongerInUseRequests.Single(r => r.CriterionItemTypeId == tickedId).Request.NoLongerInUse);
        Assert.False(apiClient.AllNoLongerInUseRequests.Single(r => r.CriterionItemTypeId == untickedId).Request.NoLongerInUse);
    }

    [Fact]
    public async Task CriterionItemTypeRemove_Blocked_RedisplaysWithMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCriterionItemTypeApiClient
        {
            DeleteResponse = new CriterionItemTypeDeleteResponse(false, "This Criterion Item is being used on 1 Scheme(s): Scheme A and cannot be removed.")
        };
        var controller = CreateController(criterionItemTypeApiClient: apiClient);

        var result = await controller.CriterionItemTypeRemove(itemId, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CriterionItemTypeViewModel>(view.Model);
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public async Task CriterionItemTypeRemove_Success_RedisplaysWithSuccessMessageAndPassesYearIdToDelete()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeCriterionItemTypeApiClient { DeleteResponse = new CriterionItemTypeDeleteResponse(true, null) };
        var controller = CreateController(criterionItemTypeApiClient: apiClient);

        var result = await controller.CriterionItemTypeRemove(itemId, CurrentYearId, 1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CriterionItemTypeViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Equal(itemId, apiClient.LastDeletedCriterionItemTypeId);
        Assert.Equal(CurrentYearId, apiClient.LastDeletedYearId);
    }

    // Matches legacy GridViewCriterionItemType.AllowPaging/PageSize = 15.
    [Fact]
    public async Task CriterionItemType_Get_WithMoreThan15Items_PaginatesAtLegacyPageSizeOf15()
    {
        var items = Enumerable.Range(1, 16)
            .Select(i => new CriterionItemTypeResponse(Guid.NewGuid(), $"Item {i:D2}", CurrentYearId, 9999, false, 0, string.Empty))
            .ToList();
        var apiClient = new FakeCriterionItemTypeApiClient { Items = items };
        var controller = CreateController(criterionItemTypeApiClient: apiClient);

        var page1Result = await controller.CriterionItemType(CurrentYearId, null, null, page: 1, cancellationToken: CancellationToken.None);
        var page1Model = Assert.IsType<CriterionItemTypeViewModel>(Assert.IsType<ViewResult>(page1Result).Model);

        var page2Result = await controller.CriterionItemType(CurrentYearId, null, null, page: 2, cancellationToken: CancellationToken.None);
        var page2Model = Assert.IsType<CriterionItemTypeViewModel>(Assert.IsType<ViewResult>(page2Result).Model);

        Assert.Equal(15, page1Model.Rows.Count);
        Assert.Single(page2Model.Rows);
        Assert.Equal(16, page1Model.TotalRecords);
        Assert.Equal(16, page2Model.TotalRecords);
    }

    private static readonly Guid ReturnTypeId = Guid.NewGuid();

    private static FakeLookupApiClient CreateLookupApiClientWithReturnTypes() => new()
    {
        Years = [new YearResponse(CurrentYearId, "2026"), new YearResponse(CurrentYearId + 1, "2027")],
        TestReturnValueTypes = [new TestReturnValueTypeResponse(ReturnTypeId, "Numeric")]
    };

    [Fact]
    public async Task TestResultItemType_Get_BuildsRowsYearOptionsAndReturnTypeOptions()
    {
        var apiClient = new FakeTestResultItemTypeApiClient
        {
            Items = [new TestResultItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)]
        };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestResultItemType(null, null, null, search: null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestResultItemTypeViewModel>(view.Model);
        Assert.Single(model.Rows);
        Assert.Equal("Numeric", model.Rows[0].TestReturnValueTypeName);
        Assert.Single(model.ReturnValueTypeOptions);
    }

    [Fact]
    public async Task TestResultItemType_Get_WithEditIdOnInUseItem_RefusesEditModeAndShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestResultItemTypeApiClient
        {
            Items = [new TestResultItemTypeResponse(itemId, Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")]
        };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestResultItemType(CurrentYearId, itemId, null, search: null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestResultItemTypeViewModel>(view.Model);
        Assert.False(model.Rows[0].IsEditing);
        Assert.True(model.MessageIsError);
        Assert.Contains("Scheme A, Scheme B", model.Message);
    }

    // AC: "Given a year is selected and the search field is left blank, When Search is selected,
    // Then all Test Result Items for that year are displayed."
    [Fact]
    public async Task TestResultItemType_Get_BlankSearch_PassesNullSearchToApiClient()
    {
        var apiClient = new FakeTestResultItemTypeApiClient { Items = [] };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        await controller.TestResultItemType(CurrentYearId, null, null, search: null, cancellationToken: CancellationToken.None);

        Assert.Null(apiClient.LastSearch);
    }

    // AC: "Given a name is entered, When Search is selected, Then only matching Test Result Items
    // are returned."
    [Fact]
    public async Task TestResultItemType_Get_WithSearch_PassesSearchTextToApiClientAndPreservesItOnTheViewModel()
    {
        var apiClient = new FakeTestResultItemTypeApiClient { Items = [] };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestResultItemType(CurrentYearId, null, null, search: "weig", cancellationToken: CancellationToken.None);

        Assert.Equal("weig", apiClient.LastSearch);
        var model = Assert.IsType<TestResultItemTypeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("weig", model.SearchText);
    }

    [Fact]
    public async Task TestResultItemTypeAdd_ValidModel_CreatesAndRedirects()
    {
        var apiClient = new FakeTestResultItemTypeApiClient();
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestResultItemTypeAddViewModel { Name = "Weight", TestReturnValueTypeId = ReturnTypeId, NoLongerInUse = false };

        var result = await controller.TestResultItemTypeAdd(model, CurrentYearId, 1, search: null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.TestResultItemType), redirect.ActionName);
        Assert.Equal("Weight", apiClient.LastCreateRequest?.Name);
        Assert.Equal(ReturnTypeId, apiClient.LastCreateRequest?.TestReturnValueTypeId);
    }

    [Fact]
    public async Task TestResultItemTypeAdd_InvalidModel_MissingReturnType_RedisplaysWithValidationError()
    {
        var apiClient = new FakeTestResultItemTypeApiClient();
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestResultItemTypeAddViewModel { Name = "Weight", TestReturnValueTypeId = null };
        controller.ModelState.AddModelError("Add.TestReturnValueTypeId", "Select a return type");

        var result = await controller.TestResultItemTypeAdd(model, CurrentYearId, 1, search: null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Null(apiClient.LastCreateRequest);
    }

    [Fact]
    public async Task TestResultItemTypeAdd_ApiReportsFieldError_RedisplaysWithModelStateError()
    {
        var apiClient = new FakeTestResultItemTypeApiClient
        {
            SaveResult = new TestResultItemTypeSaveResult(false, null, new Dictionary<string, string[]> { ["Name"] = ["Name already in use this year."] })
        };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestResultItemTypeAddViewModel { Name = "Weight", TestReturnValueTypeId = ReturnTypeId };

        var result = await controller.TestResultItemTypeAdd(model, CurrentYearId, 1, search: null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Add.Name"));
    }

    [Fact]
    public async Task TestResultItemTypeSave_ValidModel_UpdatesAndRedirects()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestResultItemTypeApiClient();
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestResultItemTypeEditViewModel { TestResultItemTypeId = itemId, Name = "Weight (renamed)", TestReturnValueTypeId = ReturnTypeId, NoLongerInUse = true };

        var result = await controller.TestResultItemTypeSave(model, CurrentYearId, 1, search: null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.TestResultItemType), redirect.ActionName);
        Assert.Equal(itemId, apiClient.LastUpdateRequest?.TestResultItemTypeId);
    }

    [Fact]
    public async Task TestResultItemTypeSave_ApiReportsInUseConflict_RedisplaysWithBannerMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestResultItemTypeApiClient
        {
            SaveResult = new TestResultItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This Test Result Item is being used on 1 Scheme(s): Scheme A and cannot be edited."] })
        };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestResultItemTypeEditViewModel { TestResultItemTypeId = itemId, Name = "Weight", TestReturnValueTypeId = ReturnTypeId };

        var result = await controller.TestResultItemTypeSave(model, CurrentYearId, 1, search: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<TestResultItemTypeViewModel>(view.Model);
        Assert.True(resultModel.MessageIsError);
    }

    [Fact]
    public async Task TestResultItemTypeSave_ApiReportsFieldError_RedisplaysWithModelStateError()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestResultItemTypeApiClient
        {
            SaveResult = new TestResultItemTypeSaveResult(false, null, new Dictionary<string, string[]> { ["Name"] = ["Name already in use this year."] })
        };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestResultItemTypeEditViewModel { TestResultItemTypeId = itemId, Name = "Weight", TestReturnValueTypeId = ReturnTypeId };

        var result = await controller.TestResultItemTypeSave(model, CurrentYearId, 1, search: null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Edit.Name"));
    }

    [Fact]
    public async Task TestResultItemTypeSaveAllNoLongerInUse_TickedAndUntickedRows_SavesBothCorrectly()
    {
        var tickedId = Guid.NewGuid();
        var untickedId = Guid.NewGuid();
        var apiClient = new FakeTestResultItemTypeApiClient();
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestResultItemTypeSaveAllNoLongerInUse(
            pageTestResultItemTypeIds: [tickedId, untickedId],
            checkedTestResultItemTypeIds: [tickedId],
            CurrentYearId, 1, search: null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(apiClient.AllNoLongerInUseRequests.Single(r => r.TestResultItemTypeId == tickedId).Request.NoLongerInUse);
        Assert.False(apiClient.AllNoLongerInUseRequests.Single(r => r.TestResultItemTypeId == untickedId).Request.NoLongerInUse);
    }

    [Fact]
    public async Task TestResultItemTypeSaveAllNoLongerInUse_ApiThrowsTransientFailure_RedisplaysWithFailureMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestResultItemTypeApiClient
        {
            Items = [new TestResultItemTypeResponse(itemId, Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)],
            SetNoLongerInUseException = new HttpRequestException("Connection refused")
        };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestResultItemTypeSaveAllNoLongerInUse(
            pageTestResultItemTypeIds: [itemId],
            checkedTestResultItemTypeIds: [itemId],
            CurrentYearId, 1, search: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestResultItemTypeViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Contains("did not respond", model.Message);
    }

    [Fact]
    public async Task TestResultItemTypeRemove_Blocked_RedisplaysWithMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestResultItemTypeApiClient
        {
            DeleteResponse = new TestResultItemTypeDeleteResponse(false, "This Test Result Item is being used on 1 Scheme(s): Scheme A and cannot be removed.")
        };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestResultItemTypeRemove(itemId, CurrentYearId, 1, search: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestResultItemTypeViewModel>(view.Model);
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public async Task TestResultItemTypeRemove_Success_RedisplaysWithSuccessMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestResultItemTypeApiClient { DeleteResponse = new TestResultItemTypeDeleteResponse(true, null) };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestResultItemTypeRemove(itemId, CurrentYearId, 1, search: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestResultItemTypeViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Equal(itemId, apiClient.LastDeletedTestResultItemTypeId);
    }

    // Matches legacy GridViewTestResultItemType.AllowPaging/PageSize = 15.
    [Fact]
    public async Task TestResultItemType_Get_WithMoreThan15Items_PaginatesAtLegacyPageSizeOf15()
    {
        var items = Enumerable.Range(1, 16)
            .Select(i => new TestResultItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), $"Item {i:D2}", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty))
            .ToList();
        var apiClient = new FakeTestResultItemTypeApiClient { Items = items };
        var controller = CreateController(testResultItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var page1Result = await controller.TestResultItemType(CurrentYearId, null, null, search: null, page: 1, cancellationToken: CancellationToken.None);
        var page1Model = Assert.IsType<TestResultItemTypeViewModel>(Assert.IsType<ViewResult>(page1Result).Model);

        var page2Result = await controller.TestResultItemType(CurrentYearId, null, null, search: null, page: 2, cancellationToken: CancellationToken.None);
        var page2Model = Assert.IsType<TestResultItemTypeViewModel>(Assert.IsType<ViewResult>(page2Result).Model);

        Assert.Equal(15, page1Model.Rows.Count);
        Assert.Single(page2Model.Rows);
        Assert.Equal(16, page1Model.TotalRecords);
        Assert.Equal(16, page2Model.TotalRecords);
    }

    [Fact]
    public async Task TestMethodItemType_Get_BuildsRowsYearOptionsAndReturnTypeOptions()
    {
        var apiClient = new FakeTestMethodItemTypeApiClient
        {
            Items = [new TestMethodItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Titration", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)]
        };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestMethodItemType(null, null, null, search: null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestMethodItemTypeViewModel>(view.Model);
        Assert.Single(model.Rows);
        Assert.Equal("Numeric", model.Rows[0].TestReturnValueTypeName);
        Assert.Single(model.ReturnValueTypeOptions);
    }

    [Fact]
    public async Task TestMethodItemType_Get_WithEditIdOnInUseItem_RefusesEditModeAndShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestMethodItemTypeApiClient
        {
            Items = [new TestMethodItemTypeResponse(itemId, Guid.NewGuid(), "Titration", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")]
        };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestMethodItemType(CurrentYearId, itemId, null, search: null, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestMethodItemTypeViewModel>(view.Model);
        Assert.False(model.Rows[0].IsEditing);
        Assert.True(model.MessageIsError);
        Assert.Contains("Scheme A, Scheme B", model.Message);
    }

    // AC: "Given a year is selected and the search field is left blank, When Search is selected,
    // Then all Test Method Items for that year are displayed."
    [Fact]
    public async Task TestMethodItemType_Get_BlankSearch_PassesNullSearchToApiClient()
    {
        var apiClient = new FakeTestMethodItemTypeApiClient { Items = [] };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        await controller.TestMethodItemType(CurrentYearId, null, null, search: null, cancellationToken: CancellationToken.None);

        Assert.Null(apiClient.LastSearch);
    }

    [Fact]
    public async Task TestMethodItemType_Get_WithSearch_PassesSearchTextToApiClientAndPreservesItOnTheViewModel()
    {
        var apiClient = new FakeTestMethodItemTypeApiClient { Items = [] };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestMethodItemType(CurrentYearId, null, null, search: "titr", cancellationToken: CancellationToken.None);

        Assert.Equal("titr", apiClient.LastSearch);
        var model = Assert.IsType<TestMethodItemTypeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("titr", model.SearchText);
    }

    // AC: "Given the administrator creates a Test Method Item with a Return Type, When Save is
    // selected, Then the item is stored and available for assignment to tests."
    [Fact]
    public async Task TestMethodItemTypeAdd_ValidModel_CreatesAndRedirects()
    {
        var apiClient = new FakeTestMethodItemTypeApiClient();
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestMethodItemTypeAddViewModel { Name = "Titration", TestReturnValueTypeId = ReturnTypeId, NoLongerInUse = false };

        var result = await controller.TestMethodItemTypeAdd(model, CurrentYearId, 1, search: null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.TestMethodItemType), redirect.ActionName);
        Assert.Equal("Titration", apiClient.LastCreateRequest?.Name);
        Assert.Equal(ReturnTypeId, apiClient.LastCreateRequest?.TestReturnValueTypeId);
    }

    [Fact]
    public async Task TestMethodItemTypeAdd_InvalidModel_MissingReturnType_RedisplaysWithValidationError()
    {
        var apiClient = new FakeTestMethodItemTypeApiClient();
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestMethodItemTypeAddViewModel { Name = "Titration", TestReturnValueTypeId = null };
        controller.ModelState.AddModelError("Add.TestReturnValueTypeId", "Select a return type");

        var result = await controller.TestMethodItemTypeAdd(model, CurrentYearId, 1, search: null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Null(apiClient.LastCreateRequest);
    }

    [Fact]
    public async Task TestMethodItemTypeAdd_ApiReportsFieldError_RedisplaysWithModelStateError()
    {
        var apiClient = new FakeTestMethodItemTypeApiClient
        {
            SaveResult = new TestMethodItemTypeSaveResult(false, null, new Dictionary<string, string[]> { ["Name"] = ["Name already in use this year."] })
        };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestMethodItemTypeAddViewModel { Name = "Titration", TestReturnValueTypeId = ReturnTypeId };

        var result = await controller.TestMethodItemTypeAdd(model, CurrentYearId, 1, search: null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Add.Name"));
    }

    [Fact]
    public async Task TestMethodItemTypeSave_ValidModel_UpdatesAndRedirects()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestMethodItemTypeApiClient();
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestMethodItemTypeEditViewModel { TestMethodItemTypeId = itemId, Name = "Titration (renamed)", TestReturnValueTypeId = ReturnTypeId, NoLongerInUse = true };

        var result = await controller.TestMethodItemTypeSave(model, CurrentYearId, 1, search: null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeAdminController.TestMethodItemType), redirect.ActionName);
        Assert.Equal(itemId, apiClient.LastUpdateRequest?.TestMethodItemTypeId);
    }

    [Fact]
    public async Task TestMethodItemTypeSave_ApiReportsInUseConflict_RedisplaysWithBannerMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestMethodItemTypeApiClient
        {
            SaveResult = new TestMethodItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This Method Item is being used on 1 Scheme(s): Scheme A and cannot be edited."] })
        };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestMethodItemTypeEditViewModel { TestMethodItemTypeId = itemId, Name = "Titration", TestReturnValueTypeId = ReturnTypeId };

        var result = await controller.TestMethodItemTypeSave(model, CurrentYearId, 1, search: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<TestMethodItemTypeViewModel>(view.Model);
        Assert.True(resultModel.MessageIsError);
    }

    [Fact]
    public async Task TestMethodItemTypeSave_ApiReportsFieldError_RedisplaysWithModelStateError()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestMethodItemTypeApiClient
        {
            SaveResult = new TestMethodItemTypeSaveResult(false, null, new Dictionary<string, string[]> { ["Name"] = ["Name already in use this year."] })
        };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());
        var model = new TestMethodItemTypeEditViewModel { TestMethodItemTypeId = itemId, Name = "Titration", TestReturnValueTypeId = ReturnTypeId };

        var result = await controller.TestMethodItemTypeSave(model, CurrentYearId, 1, search: null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Edit.Name"));
    }

    [Fact]
    public async Task TestMethodItemTypeSaveAllNoLongerInUse_TickedAndUntickedRows_SavesBothCorrectly()
    {
        var tickedId = Guid.NewGuid();
        var untickedId = Guid.NewGuid();
        var apiClient = new FakeTestMethodItemTypeApiClient();
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestMethodItemTypeSaveAllNoLongerInUse(
            pageTestMethodItemTypeIds: [tickedId, untickedId],
            checkedTestMethodItemTypeIds: [tickedId],
            CurrentYearId, 1, search: null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(apiClient.AllNoLongerInUseRequests.Single(r => r.TestMethodItemTypeId == tickedId).Request.NoLongerInUse);
        Assert.False(apiClient.AllNoLongerInUseRequests.Single(r => r.TestMethodItemTypeId == untickedId).Request.NoLongerInUse);
    }

    [Fact]
    public async Task TestMethodItemTypeSaveAllNoLongerInUse_ApiThrowsTransientFailure_RedisplaysWithFailureMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestMethodItemTypeApiClient
        {
            Items = [new TestMethodItemTypeResponse(itemId, Guid.NewGuid(), "Titration", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)],
            SetNoLongerInUseException = new HttpRequestException("Connection refused")
        };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestMethodItemTypeSaveAllNoLongerInUse(
            pageTestMethodItemTypeIds: [itemId],
            checkedTestMethodItemTypeIds: [itemId],
            CurrentYearId, 1, search: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestMethodItemTypeViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Contains("did not respond", model.Message);
    }

    // AC: "Given a method item is in use on a distribution, When removal is attempted, Then the
    // action is prevented and a message identifying the dependency is displayed."
    [Fact]
    public async Task TestMethodItemTypeRemove_Blocked_RedisplaysWithMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestMethodItemTypeApiClient
        {
            DeleteResponse = new TestMethodItemTypeDeleteResponse(false, "This Method Item is being used on 1 Scheme(s): Scheme A and cannot be removed.")
        };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestMethodItemTypeRemove(itemId, CurrentYearId, 1, search: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestMethodItemTypeViewModel>(view.Model);
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public async Task TestMethodItemTypeRemove_Success_RedisplaysWithSuccessMessage()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeTestMethodItemTypeApiClient { DeleteResponse = new TestMethodItemTypeDeleteResponse(true, null) };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var result = await controller.TestMethodItemTypeRemove(itemId, CurrentYearId, 1, search: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestMethodItemTypeViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Equal(itemId, apiClient.LastDeletedTestMethodItemTypeId);
    }

    // Matches legacy GridViewTestMethodItemType.AllowPaging/PageSize = 15.
    [Fact]
    public async Task TestMethodItemType_Get_WithMoreThan15Items_PaginatesAtLegacyPageSizeOf15()
    {
        var items = Enumerable.Range(1, 16)
            .Select(i => new TestMethodItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), $"Item {i:D2}", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty))
            .ToList();
        var apiClient = new FakeTestMethodItemTypeApiClient { Items = items };
        var controller = CreateController(testMethodItemTypeApiClient: apiClient, lookupApiClient: CreateLookupApiClientWithReturnTypes());

        var page1Result = await controller.TestMethodItemType(CurrentYearId, null, null, search: null, page: 1, cancellationToken: CancellationToken.None);
        var page1Model = Assert.IsType<TestMethodItemTypeViewModel>(Assert.IsType<ViewResult>(page1Result).Model);

        var page2Result = await controller.TestMethodItemType(CurrentYearId, null, null, search: null, page: 2, cancellationToken: CancellationToken.None);
        var page2Model = Assert.IsType<TestMethodItemTypeViewModel>(Assert.IsType<ViewResult>(page2Result).Model);

        Assert.Equal(15, page1Model.Rows.Count);
        Assert.Single(page2Model.Rows);
        Assert.Equal(16, page1Model.TotalRecords);
        Assert.Equal(16, page2Model.TotalRecords);
    }
}
