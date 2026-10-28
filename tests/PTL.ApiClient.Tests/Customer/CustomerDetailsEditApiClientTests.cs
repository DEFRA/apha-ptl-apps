using System.Net;
using PTL.ApiClient.Tests;
using PTL.Contracts.Customer;

namespace PTL.ApiClient.Tests.Customer;

public class CustomerDetailsEditApiClientTests
{
    private static CustomerDetailsEditApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_Success_ReturnsDeserializedResponse()
    {
        const string json = """
            {"qalNumber":"QAL/00001","hasOutstandingUpdate":false,"contactName":"Jane Doe",
             "organisation":"Test Lab","address1":"1 Test Street","address2":"Testville","address3":"",
             "address4":"","address5":"","countryId":"11111111-1111-1111-1111-111111111111",
             "telephone":"01234 567890","telephone2":"","email":"jane@example.com","invoiceName":"",
             "invoiceOrganisation":"Test Lab Invoicing","invoiceAddress1":"1 Test Street",
             "invoiceAddress2":"Testville","invoiceAddress3":"","invoiceAddress4":"","invoiceAddress5":"",
             "invoiceCountryId":"22222222-2222-2222-2222-222222222222","invoiceTelephone":"",
             "invoiceTelephone2":"","invoiceEmail":""}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("Jane Doe", result!.ContactName);
        Assert.Equal("QAL/00001", result.QalNumber);
    }

    [Fact]
    public async Task SubmitAsync_Success_ReturnsDeserializedResult()
    {
        const string json = """{"success":true,"errors":{}}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SubmitAsync(Guid.NewGuid(), new CustomerDetailsEditSaveRequest(
            "Jane Doe", "Test Lab", "1 Test Street", "Testville", "", "", "",
            Guid.NewGuid(), "01234 567890", "", "jane@example.com",
            "", "Test Lab Invoicing", "1 Test Street", "Testville", "", "", "",
            Guid.NewGuid(), "", "", ""));

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task SubmitAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = """{"success":false,"errors":{"ContactName":["Enter Contact Name"]}}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SubmitAsync(Guid.NewGuid(), new CustomerDetailsEditSaveRequest(
            "", "", "", "", "", "", "", Guid.Empty, "", "", "",
            "", "", "", "", "", "", "", Guid.Empty, "", "", ""));

        Assert.False(result.Success);
        Assert.Contains("ContactName", result.Errors.Keys);
    }

    [Fact]
    public async Task SubmitAsync_NullResponse_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SubmitAsync(Guid.NewGuid(), new CustomerDetailsEditSaveRequest(
            "", "", "", "", "", "", "", Guid.Empty, "", "", "",
            "", "", "", "", "", "", "", Guid.Empty, "", "", ""));

        Assert.False(result.Success);
    }
}
