using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Lookup;
using PTL.InternalWeb.Tests.TestSupport;
using CustomerController = PTL.InternalWeb.Features.Customer.CustomerController;
using CustomerFormViewModel = PTL.InternalWeb.Features.Customer.CustomerFormViewModel;
using ParticipantController = PTL.InternalWeb.Features.Participant.ParticipantController;
using ParticipantFormViewModel = PTL.InternalWeb.Features.Participant.ParticipantFormViewModel;

namespace PTL.InternalWeb.Tests.Features.Labels;

// Legacy Customer.aspx ButtonPrintContactLabel/ButtonPrintInvoiceLabel and Participant.aspx
// ButtonPrintContactLabel: print from the current form values, without saving, once validation passes.
public class AddressLabelActionTests
{
    private static readonly Guid CountryId = Guid.NewGuid();

    private static FakeLookupApiClient Lookups() =>
        new() { Countries = [new CountryResponse(CountryId, "United Kingdom")] };

    private static CustomerController CreateCustomerController(FakeCustomerApiClient? apiClient = null) =>
        new(apiClient ?? new FakeCustomerApiClient(), Lookups(), NullLogger<CustomerController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static ParticipantController CreateParticipantController(FakeParticipantApiClient? apiClient = null) =>
        new(apiClient ?? new FakeParticipantApiClient(), new FakeCustomerApiClient(), Lookups(), NullLogger<ParticipantController>.Instance);

    private static string PdfText(IActionResult result)
    {
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        return Encoding.Latin1.GetString(file.FileContents);
    }

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

    [Fact]
    public async Task PrintContactLabel_Post_WithoutCustomerId_ReturnsPdfFromFormValues()
    {
        var controller = CreateCustomerController();

        var result = await controller.PrintContactLabel(SampleForm(), CancellationToken.None);

        var text = PdfText(result);
        Assert.Contains("(Alice Example) Tj", text, StringComparison.Ordinal);
        Assert.Contains("(Sample Labs) Tj", text, StringComparison.Ordinal);
        Assert.Contains("(1 Sample Street) Tj", text, StringComparison.Ordinal);
        Assert.Contains("(United Kingdom) Tj", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintInvoiceLabel_Post_UsesInvoiceFieldsNotContactFields()
    {
        var controller = CreateCustomerController();

        var result = await controller.PrintInvoiceLabel(SampleForm(), CancellationToken.None);

        var text = PdfText(result);
        Assert.Contains("(Accounts Payable) Tj", text, StringComparison.Ordinal);
        Assert.Contains("(2 Invoice Road) Tj", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(Alice Example) Tj", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintContactLabel_Post_InvalidModelState_ReturnsCreateViewAndNoPdf()
    {
        var controller = CreateCustomerController();
        controller.ModelState.AddModelError("Name", "Enter Name");

        var result = await controller.PrintContactLabel(new CustomerFormViewModel(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Create", view.ViewName);
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
    public async Task PrintAddressLabel_Post_IncludesTelephoneAfterCountry()
    {
        var controller = CreateParticipantController();
        var model = new ParticipantFormViewModel
        {
            ContactName = "Alice Example",
            Organisation = "Sample Labs",
            Address1 = "1 Sample Street",
            CountryId = CountryId,
            Telephone = "01234 567890"
        };

        var result = await controller.PrintAddressLabel(model, CancellationToken.None);

        var text = PdfText(result);
        Assert.True(
            text.IndexOf("(United Kingdom) Tj", StringComparison.Ordinal) <
            text.IndexOf("(01234 567890) Tj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrintAddressLabel_Post_InvalidModelState_ReturnsCreateViewAndNoPdf()
    {
        var controller = CreateParticipantController();
        controller.ModelState.AddModelError("LabName", "Enter a lab name");

        var result = await controller.PrintAddressLabel(new ParticipantFormViewModel(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Create", view.ViewName);
    }

    [Fact]
    public async Task PrintAddressLabel_Get_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateParticipantController(new FakeParticipantApiClient { ParticipantResponse = null });

        var result = await controller.PrintAddressLabel(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
