using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Customer;
using PTL.InternalWeb.Notifications;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Customer;

public class CustomerControllerTests
{
    private static PTL.InternalWeb.Features.Customer.CustomerController CreateController(FakeCustomerApiClient apiClient) =>
        new(apiClient, new FakeLookupApiClient(), NullLogger<PTL.InternalWeb.Features.Customer.CustomerController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static CustomerResponse SampleCustomer(Guid customerId, bool isActive = true) => new(
        customerId, "QAL/00001", string.Empty, "Sample Laboratories Ltd", string.Empty, Guid.NewGuid(), string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, "Alice Example", "Sample Laboratories Ltd", "1 Sample Street",
        "Sample District", string.Empty, string.Empty, string.Empty, Guid.NewGuid(), "01234 567890", string.Empty,
        string.Empty, "alice@example.com", Guid.NewGuid(), string.Empty, DateTime.UtcNow, string.Empty, false,
        string.Empty, "Sample Laboratories Ltd", "1 Sample Street", "Sample District", string.Empty, string.Empty,
        string.Empty, Guid.NewGuid(), string.Empty, string.Empty, string.Empty, string.Empty, isActive, false,
        isActive ? null : DateTime.UtcNow, isActive ? null : Guid.NewGuid());

    [Fact]
    public async Task Index_ReturnsViewWithSearchResults()
    {
        var customerId = Guid.NewGuid();
        var apiClient = new FakeCustomerApiClient
        {
            SearchResponse = new CustomerSearchResponse(
                [new CustomerSummaryResponse(customerId, "QAL/00001", "Sample Laboratories Ltd", "Sample Laboratories Ltd", true)],
                1, 1, 20)
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index("Sample", CustomerStatusFilter.Active, 1, 20, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Customer.CustomerListViewModel>(view.Model);
        Assert.Single(model.Customers);
        Assert.Equal(1, model.TotalCount);
    }

    [Fact]
    public async Task Details_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient { CustomerResponse = null });

        var result = await controller.Details(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ExistingCustomer_ReturnsViewWithModel()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) });

        var result = await controller.Details(customerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Customer.CustomerDetailsViewModel>(view.Model);
        Assert.Equal(customerId, model.Customer.CustomerId);
    }

    [Fact]
    public async Task Create_Get_ReturnsEmptyFormViewModel()
    {
        var controller = CreateController(new FakeCustomerApiClient());

        var result = await controller.Create(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<PTL.InternalWeb.Features.Customer.CustomerFormViewModel>(view.Model);
    }

    [Fact]
    public async Task Create_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeCustomerApiClient());
        controller.ModelState.AddModelError("Name", "Enter a name.");
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel();

        var result = await controller.Create(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Create_Post_ApiValidationFailure_ReturnsViewWithFieldErrors()
    {
        var apiClient = new FakeCustomerApiClient
        {
            SaveResult = new CustomerSaveResult(false, null, new Dictionary<string, string[]> { ["Name"] = ["Enter a name."] })
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { Name = string.Empty };

        var result = await controller.Create(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Create_Post_Success_RedirectsToDetails()
    {
        var customerId = Guid.NewGuid();
        var apiClient = new FakeCustomerApiClient
        {
            SaveResult = new CustomerSaveResult(true, SampleCustomer(customerId), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { Name = "Sample Laboratories Ltd" };

        var result = await controller.Create(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Customer.CustomerController.Details), redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Edit_Get_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient { CustomerResponse = null });

        var result = await controller.Edit(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_Success_RedirectsToDetails()
    {
        var customerId = Guid.NewGuid();
        var apiClient = new FakeCustomerApiClient
        {
            SaveResult = new CustomerSaveResult(true, SampleCustomer(customerId), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { Name = "Sample Laboratories Ltd" };

        var result = await controller.Edit(customerId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Customer.CustomerController.Details), redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Edit_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeCustomerApiClient());
        controller.ModelState.AddModelError("Name", "Enter a name.");
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel();

        var result = await controller.Edit(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Edit_Post_ApiFailure_ReturnsViewWithFieldErrors()
    {
        var apiClient = new FakeCustomerApiClient
        {
            SaveResult = new CustomerSaveResult(false, null, new Dictionary<string, string[]> { ["Name"] = ["Enter a name."] })
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { Name = string.Empty };

        var result = await controller.Edit(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    private static PendingCustomerUpdateResponse SamplePendingUpdate(Guid customerId) => new(
        customerId, "New Contact", "New Organisation", "New Address 1", string.Empty, string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, string.Empty, "new@example.com", string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, Guid.NewGuid(), string.Empty, string.Empty, string.Empty, string.Empty);

    [Fact]
    public async Task ReviewPendingCustomerUpdates_ReturnsViewWithUpdates()
    {
        var customerId = Guid.NewGuid();
        var apiClient = new FakeCustomerApiClient
        {
            PendingCustomerUpdates = [new PendingCustomerUpdateSummaryResponse(customerId, "QAL/00001", "Sample Laboratories Ltd")]
        };
        var controller = CreateController(apiClient);

        var result = await controller.ReviewPendingCustomerUpdates(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Customer.PendingCustomerUpdateListViewModel>(view.Model);
        Assert.Single(model.Updates);
    }

    [Fact]
    public async Task PendingCustomerUpdateDetails_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient { PendingCustomerUpdateComparison = null });

        var result = await controller.PendingCustomerUpdateDetails(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PendingCustomerUpdateDetails_PendingUpdateExists_ReturnsViewWithAlignedComparisonRows()
    {
        var customerId = Guid.NewGuid();
        var apiClient = new FakeCustomerApiClient
        {
            PendingCustomerUpdateComparison = new PendingCustomerUpdateComparisonResponse(SampleCustomer(customerId), SamplePendingUpdate(customerId))
        };
        var controller = CreateController(apiClient);

        var result = await controller.PendingCustomerUpdateDetails(customerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Customer.PendingCustomerUpdateDetailsViewModel>(view.Model);
        Assert.Equal(12, model.CustomerDetails.Count);
        Assert.Equal(12, model.InvoiceDetails.Count);
        var contactName = model.CustomerDetails[0];
        Assert.Equal("Contact Name", contactName.Label);
        Assert.Equal("Alice Example", contactName.CurrentValue);
        Assert.Equal("New Contact", contactName.PendingValue);
        Assert.True(contactName.HasChanged);
        Assert.Equal("Invoice Contact Name", model.InvoiceDetails[0].Label);
    }

    [Fact]
    public async Task EditPendingCustomerUpdate_Get_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient { PendingCustomerUpdateComparison = null });

        var result = await controller.EditPendingCustomerUpdate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPendingCustomerUpdate_Get_PendingUpdateExists_ReturnsFormPopulatedWithPendingValues()
    {
        var customerId = Guid.NewGuid();
        var apiClient = new FakeCustomerApiClient
        {
            PendingCustomerUpdateComparison = new PendingCustomerUpdateComparisonResponse(SampleCustomer(customerId), SamplePendingUpdate(customerId))
        };
        var controller = CreateController(apiClient);

        var result = await controller.EditPendingCustomerUpdate(customerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Customer.PendingCustomerUpdateFormViewModel>(view.Model);
        Assert.Equal("New Contact", model.ContactName);
        Assert.Equal(customerId, model.CustomerId);
    }

    [Fact]
    public async Task EditPendingCustomerUpdate_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeCustomerApiClient());
        controller.ModelState.AddModelError("ContactName", "Enter a contact name");
        var model = new PTL.InternalWeb.Features.Customer.PendingCustomerUpdateFormViewModel();

        var result = await controller.EditPendingCustomerUpdate(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task EditPendingCustomerUpdate_Post_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient
        {
            ApprovePendingCustomerUpdateResult = new PendingCustomerUpdateDecisionResult(false, true, new Dictionary<string, string[]>())
        });

        var result = await controller.EditPendingCustomerUpdate(Guid.NewGuid(), new PTL.InternalWeb.Features.Customer.PendingCustomerUpdateFormViewModel(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPendingCustomerUpdate_Post_ValidationFailure_ReturnsViewWithFieldErrors()
    {
        var controller = CreateController(new FakeCustomerApiClient
        {
            ApprovePendingCustomerUpdateResult = new PendingCustomerUpdateDecisionResult(false, false, new Dictionary<string, string[]> { ["ContactName"] = ["Contact name is required."] })
        });
        var model = new PTL.InternalWeb.Features.Customer.PendingCustomerUpdateFormViewModel();

        var result = await controller.EditPendingCustomerUpdate(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task EditPendingCustomerUpdate_Post_Success_SendsAmendedValuesAndRedirectsToReviewList()
    {
        var apiClient = new FakeCustomerApiClient();
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Customer.PendingCustomerUpdateFormViewModel { ContactName = "Amended Contact" };

        var result = await controller.EditPendingCustomerUpdate(Guid.NewGuid(), model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Customer.CustomerController.ReviewPendingCustomerUpdates), redirect.ActionName);
        Assert.Equal("Amended Contact", apiClient.LastApproveRequest!.ContactName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Customer update approved successfully.", notification.Message);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient
        {
            ApprovePendingCustomerUpdateResult = new PendingCustomerUpdateDecisionResult(false, true, new Dictionary<string, string[]>())
        });

        var result = await controller.ApprovePendingCustomerUpdate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdate_ValidationFailure_RedirectsToEditPage()
    {
        var controller = CreateController(new FakeCustomerApiClient
        {
            ApprovePendingCustomerUpdateResult = new PendingCustomerUpdateDecisionResult(false, false, new Dictionary<string, string[]> { ["ContactName"] = ["Contact name is required."] })
        });

        var result = await controller.ApprovePendingCustomerUpdate(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Customer.CustomerController.EditPendingCustomerUpdate), redirect.ActionName);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdate_Success_RedirectsToReviewListWithSuccessNotification()
    {
        var controller = CreateController(new FakeCustomerApiClient());

        var result = await controller.ApprovePendingCustomerUpdate(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Customer.CustomerController.ReviewPendingCustomerUpdates), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Customer update approved successfully.", notification.Message);
    }

    [Fact]
    public async Task DeclinePendingCustomerUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient { DeclinePendingCustomerUpdateResult = false });

        var result = await controller.DeclinePendingCustomerUpdate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeclinePendingCustomerUpdate_Success_RedirectsToReviewListWithSuccessNotification()
    {
        var controller = CreateController(new FakeCustomerApiClient { DeclinePendingCustomerUpdateResult = true });

        var result = await controller.DeclinePendingCustomerUpdate(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Customer.CustomerController.ReviewPendingCustomerUpdates), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Customer update declined successfully.", notification.Message);
    }
}
