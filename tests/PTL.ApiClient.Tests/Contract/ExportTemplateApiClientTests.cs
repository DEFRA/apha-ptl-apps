using System.Net;
using PTL.ApiClient;

namespace PTL.ApiClient.Tests.Contract;

public class ExportTemplateApiClientTests
{
    private static ExportTemplateApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetTemplatesAsync_ReturnsTheList()
    {
        const string json = """
        {
          "documentType": "Contracts",
          "displayName": "Export Contracts",
          "templates": [
            { "fileId": "11111111-1111-1111-1111-111111111111", "filename": "Contract.docx", "uploadedDate": "2026-04-01T00:00:00Z", "documentType": "Contracts", "selected": true }
          ]
        }
        """;

        var result = await CreateClient(HttpStatusCode.OK, json).GetTemplatesAsync("Contracts");

        Assert.NotNull(result);
        Assert.Equal("Export Contracts", result!.DisplayName);
        Assert.True(Assert.Single(result.Templates).Selected);
    }

    [Fact]
    public async Task GetTemplatesAsync_UnknownDocumentType_ReturnsNull() =>
        Assert.Null(await CreateClient(HttpStatusCode.NotFound, null).GetTemplatesAsync("Invoices"));

    [Fact]
    public async Task UploadTemplateAsync_ReturnsTheServiceResult()
    {
        const string json = """{ "success": false, "errorMessage": "A file with this name already exists", "template": null }""";

        var result = await CreateClient(HttpStatusCode.OK, json)
            .UploadTemplateAsync("Contracts", "Contract.docx", new MemoryStream([1, 2, 3]));

        Assert.False(result.Success);
        Assert.Equal("A file with this name already exists", result.ErrorMessage);
    }

    [Fact]
    public async Task UploadTemplateAsync_UnknownDocumentType_ReportsFileNotFound()
    {
        var result = await CreateClient(HttpStatusCode.NotFound, null)
            .UploadTemplateAsync("Invoices", "Contract.docx", new MemoryStream([1]));

        Assert.False(result.Success);
        Assert.Equal("File not found", result.ErrorMessage);
    }

    [Fact]
    public async Task DownloadTemplateAsync_Missing_ReturnsNull() =>
        Assert.Null(await CreateClient(HttpStatusCode.NotFound, null).DownloadTemplateAsync(Guid.NewGuid()));

    [Fact]
    public async Task SelectTemplateAsync_NoContent_ReturnsTrue() =>
        Assert.True(await CreateClient(HttpStatusCode.NoContent, null).SelectTemplateAsync(Guid.NewGuid()));

    [Fact]
    public async Task SelectTemplateAsync_NotFound_ReturnsFalse() =>
        Assert.False(await CreateClient(HttpStatusCode.NotFound, null).SelectTemplateAsync(Guid.NewGuid()));

    [Fact]
    public async Task DeleteTemplateAsync_NoContent_ReturnsTrue() =>
        Assert.True(await CreateClient(HttpStatusCode.NoContent, null).DeleteTemplateAsync(Guid.NewGuid()));

    [Fact]
    public async Task DeleteTemplateAsync_NotFound_ReturnsFalse() =>
        Assert.False(await CreateClient(HttpStatusCode.NotFound, null).DeleteTemplateAsync(Guid.NewGuid()));
}

public class BulkExportApiClientTests
{
    private static BulkExportApiClient CreateClient(string jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetContractsAsync_DeserialisesTheList()
    {
        const string json = """
        [
          {
            "contractId": "11111111-1111-1111-1111-111111111111",
            "customerId": "22222222-2222-2222-2222-222222222222",
            "contractNumber": "UT12345", "suffix": "A", "yearId": 2026, "symbol": "\u00A3",
            "administrationCharge": 0, "discountRate": 0,
            "numberPostage": 0, "numberCourier": 0, "numberSpecialDelivery": 0,
            "postagePrice": 0, "courierPrice": 0, "specialDeliveryPrice": 0,
            "commencementDate": "2026-04-01T00:00:00Z",
            "qalNumber": "QAL/00001", "contactName": "", "organisation": "",
            "address1": "", "address2": "", "address3": "", "address4": "", "address5": "",
            "country": "", "telephone": "", "fax": "", "email": "",
            "invoiceName": "", "invoiceOrganisation": "",
            "invoiceAddress1": "", "invoiceAddress2": "", "invoiceAddress3": "", "invoiceAddress4": "", "invoiceAddress5": "",
            "invoiceCountry": "", "invoiceTelephone": "", "invoiceFax": "", "invoiceEmail": "",
            "accountNumber": "", "vatNumber": "", "vatRating": "", "purchaseOrderNumber": "",
            "totalPriceItems": 100, "discountPrice": 0, "totalPrice": 100,
            "items": [ { "identifier": "S1", "schemeName": "Salmonella", "labCode": "LAB1", "labName": "Lab One", "noOfDistributions": 4, "price": 100 } ]
          }
        ]
        """;

        var contracts = await CreateClient(json).GetContractsAsync();

        var contract = Assert.Single(contracts);
        Assert.Equal("QAL/00001", contract.QalNumber);
        Assert.Equal(100m, contract.TotalPrice);
        Assert.Equal("Salmonella", Assert.Single(contract.Items).SchemeName);
    }

    [Fact]
    public async Task GetContractsAsync_NullBody_ReturnsEmpty() =>
        Assert.Empty(await CreateClient("null").GetContractsAsync());

    [Fact]
    public async Task GetSampleAddressesAsync_NullBody_ReturnsEmpty() =>
        Assert.Empty(await CreateClient("null").GetSampleAddressesAsync());

    [Fact]
    public async Task GetRenewalsAsync_NullBody_ReturnsEmpty() =>
        Assert.Empty(await CreateClient("null").GetRenewalsAsync(nonUk: true));
}
