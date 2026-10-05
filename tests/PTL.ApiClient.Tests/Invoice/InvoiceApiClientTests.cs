using System.Net;
using PTL.ApiClient.Tests;

namespace PTL.ApiClient.Tests.Invoice;

public class InvoiceApiClientTests
{
    private static InvoiceApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetPendingAsync_ReturnsDeserializedSummary()
    {
        const string json = """
            {"financialYearId":2026,"financialYear":"2025/26","eligibleContractCount":10,"optOutContractCount":2,"nonFeePayingItemCount":3,"notificationRecipients":["ops@example.com"]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPendingAsync();

        Assert.NotNull(result);
        Assert.Equal(2026, result!.FinancialYearId);
        Assert.Equal("2025/26", result.FinancialYear);
        Assert.Equal(10, result.EligibleContractCount);
        Assert.Equal(["ops@example.com"], result.NotificationRecipients);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsDeserializedResult()
    {
        const string json = """{"success":true,"contractCount":5,"csvStorageKey":"invoices/PT_Invoices_2026-01-01-00-00-00.csv","errorMessage":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GenerateAsync();

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(5, result.ContractCount);
        Assert.Equal("invoices/PT_Invoices_2026-01-01-00-00-00.csv", result.CsvStorageKey);
    }

    [Fact]
    public async Task ResetAsync_SuccessStatusCode_CompletesWithoutThrowing()
    {
        var client = CreateClient(HttpStatusCode.OK, null);

        var exception = await Record.ExceptionAsync(() => client.ResetAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_ReturnsDeserializedList()
    {
        const string json = """[{"auditInvoiceGenerationId":"11111111-1111-1111-1111-111111111111","auditWho":"test.user","auditDate":"2026-01-15T10:00:00"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAuditHistoryAsync();

        Assert.Single(result);
        Assert.Equal("test.user", result[0].AuditWho);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_NullResponseBody_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetAuditHistoryAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task DownloadCsvAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.DownloadCsvAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadCsvAsync_UsesContentDispositionFileName()
    {
        var client = new InvoiceApiClient(new HttpClient(new StubHttpMessageHandler(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            };
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = "PT_Invoices_2026-01-01.csv"
            };
            return response;
        }))
        { BaseAddress = new Uri("http://localhost") });

        var result = await client.DownloadCsvAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("PT_Invoices_2026-01-01.csv", result!.FileName);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Content);
    }

    [Fact]
    public async Task DownloadCsvAsync_NoContentDisposition_FallsBackToGeneratedFileName()
    {
        var generationId = Guid.NewGuid();
        var client = new InvoiceApiClient(new HttpClient(new StubHttpMessageHandler(() =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([4, 5]) }))
        { BaseAddress = new Uri("http://localhost") });

        var result = await client.DownloadCsvAsync(generationId);

        Assert.NotNull(result);
        Assert.Equal($"PT_Invoices_{generationId}.csv", result!.FileName);
    }
}
