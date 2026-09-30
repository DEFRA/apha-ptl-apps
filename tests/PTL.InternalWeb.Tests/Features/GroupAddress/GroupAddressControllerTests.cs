using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Lookup;
using PTL.InternalWeb.Features.GroupAddress;
using PTL.InternalWeb.Tests.TestSupport;
using GroupAddressResponse = PTL.Contracts.GroupAddress.GroupAddressResponse;
using GroupAddressSaveResult = PTL.ApiClient.GroupAddressSaveResult;

namespace PTL.InternalWeb.Tests.Features.GroupAddress;

public class GroupAddressControllerTests
{
    private static PTL.InternalWeb.Features.GroupAddress.GroupAddressController CreateController(
        FakeGroupAddressApiClient? groupAddressApiClient = null,
        FakeLookupApiClient? lookupApiClient = null) =>
        new(groupAddressApiClient ?? new FakeGroupAddressApiClient(), lookupApiClient ?? new FakeLookupApiClient());

    private static GroupAddressResponse SampleGroupAddress(Guid groupAddressId) => new(
        groupAddressId, "PTL-001", "1 Sample Street", string.Empty, string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), "01234 567890", string.Empty);

    [Fact]
    public async Task Index_ReturnsViewWithPagedResults()
    {
        var groupAddressId = Guid.NewGuid();
        var apiClient = new FakeGroupAddressApiClient { GroupAddresses = [SampleGroupAddress(groupAddressId)] };
        var controller = CreateController(apiClient);

        var result = await controller.Index(cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<GroupAddressListViewModel>(view.Model);
        Assert.Single(model.Items);
        Assert.Equal("PTL-001", model.Items[0].Identifier);
    }

    [Fact]
    public async Task Details_UnknownGroupAddress_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Details(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ExistingGroupAddress_ReturnsViewWithCountryName()
    {
        var groupAddressId = Guid.NewGuid();
        var groupAddress = SampleGroupAddress(groupAddressId);
        var apiClient = new FakeGroupAddressApiClient { GroupAddress = groupAddress };
        var lookupApiClient = new FakeLookupApiClient { Countries = [new CountryResponse(groupAddress.CountryId, "United Kingdom")] };
        var controller = CreateController(apiClient, lookupApiClient);

        var result = await controller.Details(groupAddressId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<GroupAddressDetailsViewModel>(view.Model);
        Assert.Equal("United Kingdom", model.CountryName);
    }

    [Fact]
    public async Task Create_Get_ReturnsViewWithPopulatedCountryOptions()
    {
        var lookupApiClient = new FakeLookupApiClient { Countries = [new CountryResponse(Guid.NewGuid(), "United Kingdom")] };
        var controller = CreateController(lookupApiClient: lookupApiClient);

        var result = await controller.Create(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<GroupAddressFormViewModel>(view.Model);
        Assert.NotEmpty(model.CountryOptions);
    }

    [Fact]
    public async Task Create_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("Identifier", "Enter an identifier");
        var model = new GroupAddressFormViewModel();

        var result = await controller.Create(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Create_Post_ApiFailure_AddsFieldErrorsAndReturnsView()
    {
        var apiClient = new FakeGroupAddressApiClient
        {
            SaveResult = new GroupAddressSaveResult(false, null, new Dictionary<string, string[]> { ["Identifier"] = ["Identifier already in use"] })
        };
        var controller = CreateController(apiClient);
        var model = new GroupAddressFormViewModel { Identifier = "PTL-001", Address1 = "1 Sample Street", CountryId = Guid.NewGuid() };

        var result = await controller.Create(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Create_Post_Success_RedirectsToIndex()
    {
        var apiClient = new FakeGroupAddressApiClient
        {
            SaveResult = new GroupAddressSaveResult(true, SampleGroupAddress(Guid.NewGuid()), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new GroupAddressFormViewModel { Identifier = "PTL-001", Address1 = "1 Sample Street", CountryId = Guid.NewGuid() };

        var result = await controller.Create(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.GroupAddress.GroupAddressController.Index), redirect.ActionName);
    }

    [Fact]
    public async Task Edit_Get_UnknownGroupAddress_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Edit(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_ExistingGroupAddress_ReturnsViewWithModel()
    {
        var groupAddressId = Guid.NewGuid();
        var apiClient = new FakeGroupAddressApiClient { GroupAddress = SampleGroupAddress(groupAddressId) };
        var controller = CreateController(apiClient);

        var result = await controller.Edit(groupAddressId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<GroupAddressFormViewModel>(view.Model);
        Assert.Equal(groupAddressId, model.GroupAddressId);
    }

    [Fact]
    public async Task Edit_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("Identifier", "Enter an identifier");
        var model = new GroupAddressFormViewModel();

        var result = await controller.Edit(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Edit_Post_ApiFailure_AddsFieldErrorsAndReturnsView()
    {
        var apiClient = new FakeGroupAddressApiClient
        {
            SaveResult = new GroupAddressSaveResult(false, null, new Dictionary<string, string[]> { ["Address1"] = ["Enter address line 1"] })
        };
        var controller = CreateController(apiClient);
        var model = new GroupAddressFormViewModel { Identifier = "PTL-001", Address1 = string.Empty, CountryId = Guid.NewGuid() };

        var result = await controller.Edit(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Edit_Post_Success_RedirectsToIndex()
    {
        var groupAddressId = Guid.NewGuid();
        var apiClient = new FakeGroupAddressApiClient
        {
            SaveResult = new GroupAddressSaveResult(true, SampleGroupAddress(groupAddressId), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new GroupAddressFormViewModel { Identifier = "PTL-001", Address1 = "1 Sample Street", CountryId = Guid.NewGuid() };

        var result = await controller.Edit(groupAddressId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.GroupAddress.GroupAddressController.Index), redirect.ActionName);
    }
}
