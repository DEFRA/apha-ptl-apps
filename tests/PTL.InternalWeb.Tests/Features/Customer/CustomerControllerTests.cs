using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Customer;
using PTL.SharedUI.Notifications;
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
    public async Task Create_Post_AllOptionalFieldsPopulated_SendsEveryFieldOnRequest()
    {
        var customerId = Guid.NewGuid();
        var apiClient = new FakeCustomerApiClient
        {
            SaveResult = new CustomerSaveResult(true, SampleCustomer(customerId), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel
        {
            RegisteredFileNumber = "RFN-1",
            Name = "Sample Laboratories Ltd",
            PreviousName = "Old Name Ltd",
            CustomerTypeId = Guid.NewGuid(),
            VatNumber = "GB123456789",
            VatRatingId = Guid.NewGuid(),
            AccountNumber = "ACC-001",
            CustomerFinanceId = "FIN-001",
            ContactName = "Alice Example",
            Organisation = "Sample Laboratories Ltd",
            Address1 = "1 Sample Street",
            Address2 = "Sample District",
            Address3 = "Sample Town",
            Address4 = "Sample County",
            Address5 = "Sample Postcode",
            CountryId = Guid.NewGuid(),
            Telephone = "01234 567890",
            Telephone2 = "01234 567891",
            Fax = "01234 567892",
            Email = "alice@example.com",
            CurrencyId = Guid.NewGuid(),
            Comments = "Some comments",
            PostageArrangements = "Courier",
            PaymentNonUK = true,
            InvoiceName = "Invoice Name Ltd",
            InvoiceOrganisation = "Invoice Org Ltd",
            InvoiceAddress1 = "1 Invoice Street",
            InvoiceAddress2 = "Invoice District",
            InvoiceAddress3 = "Invoice Town",
            InvoiceAddress4 = "Invoice County",
            InvoiceAddress5 = "Invoice Postcode",
            InvoiceCountryId = Guid.NewGuid(),
            InvoiceTelephone = "09876 543210",
            InvoiceTelephone2 = "09876 543211",
            InvoiceFax = "09876 543212",
            InvoiceEmail = "invoices@example.com"
        };

        await controller.Create(model, CancellationToken.None);

        var request = apiClient.LastSaveRequest!;
        Assert.Equal("RFN-1", request.RegisteredFileNumber);
        Assert.Equal("Old Name Ltd", request.PreviousName);
        Assert.Equal(model.CustomerTypeId, request.CustomerTypeId);
        Assert.Equal("GB123456789", request.VatNumber);
        Assert.Equal(model.VatRatingId, request.VatRatingId);
        Assert.Equal("ACC-001", request.AccountNumber);
        Assert.Equal("FIN-001", request.CustomerFinanceId);
        Assert.Equal("Alice Example", request.ContactName);
        Assert.Equal("1 Sample Street", request.Address1);
        Assert.Equal("Sample District", request.Address2);
        Assert.Equal("Sample Town", request.Address3);
        Assert.Equal("Sample County", request.Address4);
        Assert.Equal("Sample Postcode", request.Address5);
        Assert.Equal(model.CountryId, request.CountryId);
        Assert.Equal("01234 567890", request.Telephone);
        Assert.Equal("01234 567891", request.Telephone2);
        Assert.Equal("01234 567892", request.Fax);
        Assert.Equal("alice@example.com", request.Email);
        Assert.Equal(model.CurrencyId, request.CurrencyId);
        Assert.Equal("Some comments", request.Comments);
        Assert.Equal("Courier", request.PostageArrangements);
        Assert.True(request.PaymentNonUK);
        Assert.Equal("Invoice Name Ltd", request.InvoiceName);
        Assert.Equal("Invoice Org Ltd", request.InvoiceOrganisation);
        Assert.Equal("1 Invoice Street", request.InvoiceAddress1);
        Assert.Equal("Invoice District", request.InvoiceAddress2);
        Assert.Equal("Invoice Town", request.InvoiceAddress3);
        Assert.Equal("Invoice County", request.InvoiceAddress4);
        Assert.Equal("Invoice Postcode", request.InvoiceAddress5);
        Assert.Equal(model.InvoiceCountryId, request.InvoiceCountryId);
        Assert.Equal("09876 543210", request.InvoiceTelephone);
        Assert.Equal("09876 543211", request.InvoiceTelephone2);
        Assert.Equal("09876 543212", request.InvoiceFax);
        Assert.Equal("invoices@example.com", request.InvoiceEmail);
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

    [Fact]
    public async Task PrintContactLabel_ById_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient { CustomerResponse = null });

        var result = await controller.PrintContactLabel(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PrintContactLabel_ById_ExistingCustomer_ReturnsContactAddressLabelPdf()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) });

        var result = await controller.PrintContactLabel(customerId, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("contact-address-label.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task PrintInvoiceLabel_ById_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerApiClient { CustomerResponse = null });

        var result = await controller.PrintInvoiceLabel(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PrintInvoiceLabel_ById_ExistingCustomer_ReturnsInvoiceAddressLabelPdf()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) });

        var result = await controller.PrintInvoiceLabel(customerId, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("invoice-address-label.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task PrintContactLabel_Post_InvalidModelState_RedisplaysForm()
    {
        var controller = CreateController(new FakeCustomerApiClient());
        controller.ModelState.AddModelError("Name", "Enter a name.");
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel();

        var result = await controller.PrintContactLabel(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task PrintContactLabel_Post_InvalidModelStateWithCustomerId_RedisplaysEditForm()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) });
        controller.ModelState.AddModelError("Name", "Enter a name.");
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { CustomerId = customerId };

        var result = await controller.PrintContactLabel(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Edit", view.ViewName);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task PrintContactLabel_Post_ValidModel_ReturnsContactAddressLabelPdf()
    {
        var controller = CreateController(new FakeCustomerApiClient());
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { ContactName = "Alice Example" };

        var result = await controller.PrintContactLabel(model, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("contact-address-label.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task PrintInvoiceLabel_Post_ValidModel_ReturnsInvoiceAddressLabelPdf()
    {
        var controller = CreateController(new FakeCustomerApiClient());
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { InvoiceName = "Alice Example" };

        var result = await controller.PrintInvoiceLabel(model, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("invoice-address-label.pdf", file.FileDownloadName);
    }
}
