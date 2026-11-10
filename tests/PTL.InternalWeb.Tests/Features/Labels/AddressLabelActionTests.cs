using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Customer;
using PTL.Contracts.Lookup;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;
using CustomerController = PTL.InternalWeb.Features.Customer.CustomerController;
using CustomerFormViewModel = PTL.InternalWeb.Features.Customer.CustomerFormViewModel;
using ParticipantController = PTL.InternalWeb.Features.Participant.ParticipantController;
using ParticipantFormViewModel = PTL.InternalWeb.Features.Participant.ParticipantFormViewModel;

namespace PTL.InternalWeb.Tests.Features.Labels;

// Legacy Customer.aspx ButtonPrintContactLabel/ButtonPrintInvoiceLabel and Participant.aspx
// ButtonPrintContactLabel: print from the current form values, without saving, once validation
// passes. Legacy sends the label straight to a physical printer configured in web.config and never
// returns a file to the browser - these actions reproduce that via ILabelPrinterApiClient (a call
// to PTL.Api, which holds the actual printer connection) rather than a PDF download.
public class AddressLabelActionTests
{
    private static readonly Guid CountryId = Guid.NewGuid();

    private static FakeLookupApiClient Lookups() =>
        new() { Countries = [new CountryResponse(CountryId, "United Kingdom")] };

    private static CustomerController CreateCustomerController(FakeCustomerApiClient? apiClient = null, FakeLabelPrinterApiClient? labelPrinterApiClient = null) =>
        new(apiClient ?? new FakeCustomerApiClient(), Lookups(), labelPrinterApiClient ?? new FakeLabelPrinterApiClient(), NullLogger<CustomerController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static ParticipantController CreateParticipantController(FakeParticipantApiClient? apiClient = null, FakeLabelPrinterApiClient? labelPrinterApiClient = null) =>
        new(apiClient ?? new FakeParticipantApiClient(), new FakeCustomerApiClient(), Lookups(), labelPrinterApiClient ?? new FakeLabelPrinterApiClient(), NullLogger<ParticipantController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static CustomerFormViewModel SampleForm() => new()
    {
        Name = "Sample Laboratories Ltd",
        ContactName = "Alice Example",
        Organisation = "Sample Labs",
        Address1 = "1 Sample Street",
        Address2 = "Sample District",
        CountryId = CountryId,
        InvoiceName = "Accounts Payable",
        InvoiceOrganisation = "Sample Labs Finance",
        InvoiceAddress1 = "2 Invoice Road",
        InvoiceCountryId = CountryId
    };

    private static CustomerResponse SampleCustomerResponse(Guid customerId) => new(
        customerId, "QAL/00001", string.Empty, "Sample Laboratories Ltd", string.Empty, Guid.NewGuid(), string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, "Alice Example", "Sample Labs", "1 Sample Street",
        "Sample District", string.Empty, string.Empty, string.Empty, CountryId, "01234 567890", string.Empty,
        string.Empty, "alice@example.com", Guid.NewGuid(), string.Empty, DateTime.UtcNow, string.Empty, false,
        "Accounts Payable", "Sample Labs Finance", "2 Invoice Road", string.Empty, string.Empty,
        string.Empty, string.Empty, CountryId, string.Empty, string.Empty, string.Empty, string.Empty, true, false,
        null, null);

    private static ParticipantResponse SampleParticipantResponse(Guid participantId) => new(
        participantId, Guid.NewGuid(), Guid.NewGuid(), "LAB1", "Lab One", Guid.NewGuid(), "Alice Example",
        "Sample Labs", "1 Sample Street", string.Empty, string.Empty, string.Empty, string.Empty,
        CountryId, "01234 567890", string.Empty, string.Empty, string.Empty, string.Empty, true, null, false, null);

    [Fact]
    public async Task PrintContactLabel_Post_WithoutCustomerId_PrintsFromFormValuesAndRedisplaysCreate()
    {
        var printer = new FakeLabelPrinterApiClient();
        var controller = CreateCustomerController(labelPrinterApiClient: printer);

        var result = await controller.PrintContactLabel(SampleForm(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Create", view.ViewName);
        var lines = Assert.Single(printer.PrintedLines);
        Assert.Contains("Alice Example", lines);
        Assert.Contains("Sample Labs", lines);
        Assert.Contains("1 Sample Street", lines);
        Assert.Contains("United Kingdom", lines);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task PrintInvoiceLabel_Post_UsesInvoiceFieldsNotContactFields()
    {
        var printer = new FakeLabelPrinterApiClient();
        var controller = CreateCustomerController(labelPrinterApiClient: printer);

        await controller.PrintInvoiceLabel(SampleForm(), CancellationToken.None);

        var lines = Assert.Single(printer.PrintedLines);
        Assert.Contains("Accounts Payable", lines);
        Assert.Contains("2 Invoice Road", lines);
        Assert.DoesNotContain("Alice Example", lines);
    }

    [Fact]
    public async Task PrintContactLabel_Post_PrinterFails_SetsErrorNotificationButStillRedisplaysForm()
    {
        var printer = new FakeLabelPrinterApiClient { PrintResult = false };
        var controller = CreateCustomerController(labelPrinterApiClient: printer);

        var result = await controller.PrintContactLabel(SampleForm(), CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(NotificationType.Error, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task PrintContactLabel_Post_InvalidModelState_ReturnsCreateViewAndDoesNotPrint()
    {
        var printer = new FakeLabelPrinterApiClient();
        var controller = CreateCustomerController(labelPrinterApiClient: printer);
        controller.ModelState.AddModelError("Name", "Enter Name");

        var result = await controller.PrintContactLabel(new CustomerFormViewModel(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Create", view.ViewName);
        Assert.Empty(printer.PrintedLines);
    }

    [Fact]
    public async Task PrintContactLabel_Post_InvalidModelState_WithCustomerId_ReturnsEditView()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateCustomerController();
        controller.ModelState.AddModelError("Name", "Enter Name");

        var result = await controller.PrintContactLabel(new CustomerFormViewModel { CustomerId = customerId }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Edit", view.ViewName);
    }

    [Fact]
    public async Task PrintContactLabel_Get_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateCustomerController(new FakeCustomerApiClient { CustomerResponse = null });

        var result = await controller.PrintContactLabel(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PrintContactLabel_Get_KnownCustomer_PrintsAndRedirectsToDetailsWithSuccessNotification()
    {
        var customerId = Guid.NewGuid();
        var printer = new FakeLabelPrinterApiClient();
        var controller = CreateCustomerController(new FakeCustomerApiClient { CustomerResponse = SampleCustomerResponse(customerId) }, printer);

        var result = await controller.PrintContactLabel(customerId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["id"]);
        Assert.Single(printer.PrintedLines);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task PrintAddressLabel_Post_IncludesTelephoneAfterCountry()
    {
        var printer = new FakeLabelPrinterApiClient();
        var controller = CreateParticipantController(labelPrinterApiClient: printer);
        var model = new ParticipantFormViewModel
        {
            ContactName = "Alice Example",
            Organisation = "Sample Labs",
            Address1 = "1 Sample Street",
            CountryId = CountryId,
            Telephone = "01234 567890"
        };

        await controller.PrintAddressLabel(model, CancellationToken.None);

        var lines = Assert.Single(printer.PrintedLines).ToList();
        Assert.True(lines.IndexOf("United Kingdom") < lines.IndexOf("01234 567890"));
    }

    [Fact]
    public async Task PrintAddressLabel_Post_InvalidModelState_ReturnsCreateViewAndDoesNotPrint()
    {
        var printer = new FakeLabelPrinterApiClient();
        var controller = CreateParticipantController(labelPrinterApiClient: printer);
        controller.ModelState.AddModelError("LabName", "Enter a lab name");

        var result = await controller.PrintAddressLabel(new ParticipantFormViewModel(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Create", view.ViewName);
        Assert.Empty(printer.PrintedLines);
    }

    [Fact]
    public async Task PrintAddressLabel_Get_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateParticipantController(new FakeParticipantApiClient { ParticipantResponse = null });

        var result = await controller.PrintAddressLabel(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PrintAddressLabel_Get_KnownParticipant_PrintsAndRedirectsToDetailsWithSuccessNotification()
    {
        var participantId = Guid.NewGuid();
        var printer = new FakeLabelPrinterApiClient();
        var controller = CreateParticipantController(new FakeParticipantApiClient { ParticipantResponse = SampleParticipantResponse(participantId) }, printer);

        var result = await controller.PrintAddressLabel(participantId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(participantId, redirect.RouteValues!["id"]);
        Assert.Single(printer.PrintedLines);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }
}

