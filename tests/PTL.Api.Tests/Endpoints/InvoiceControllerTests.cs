using Microsoft.Extensions.Options;
using PTL.Api.Controllers;
using PTL.Api.Tests.Invoice;
using PTL.Core.Invoice;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.Endpoints;

public class InvoiceControllerTests
{
    private static InvoiceController CreateController(
        FakeInvoiceRepository repository,
        FakeLookupServiceForInvoice? lookupService = null,
        IReadOnlyList<string>? recipients = null)
    {
        var service = new InvoiceService(
            repository,
            new FakeInvoiceStorageService(),
            new FakeNotifyClient(),
            Options.Create(new InvoiceStorageOptions()),
            Options.Create(new InvoiceNotificationOptions { TemplateId = "template-1", Recipients = recipients ?? ["ops@example.com"] }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceService>.Instance);

        return new InvoiceController(service, lookupService ?? new FakeLookupServiceForInvoice(), Options.Create(new InvoiceNotificationOptions { Recipients = recipients ?? ["ops@example.com"] }));
    }

    [Fact]
    public async Task GetPending_ReturnsCurrentYearAndSummary()
    {
        var repository = new FakeInvoiceRepository();
        var contractId = Guid.NewGuid();
        repository.PendingData = new InvoicePendingData(
            [new InvoiceContractEntity { ContractId = contractId, OptOutOfInvoiceGeneration = true }],
            [new InvoiceContractItemEntity { ContractId = contractId, NonFeePaying = true }]);
        var lookup = new FakeLookupServiceForInvoice { CurrentYears = [new YearEntity { YearId = 2026, Year = "2025/26" }] };
        var controller = CreateController(repository, lookup, recipients: ["ops@example.com"]);

        var result = await controller.GetPending(CancellationToken.None);

        var response = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value;
        var summary = Assert.IsType<PTL.Contracts.Invoice.PendingInvoiceSummaryResponse>(response);
        Assert.Equal(2026, summary.FinancialYearId);
        Assert.Equal("2025/26", summary.FinancialYear);
        Assert.Equal(1, summary.EligibleContractCount);
        Assert.Equal(1, summary.OptOutContractCount);
        Assert.Equal(1, summary.NonFeePayingItemCount);
        Assert.Equal(["ops@example.com"], summary.NotificationRecipients);
    }

    [Fact]
    public async Task GetPending_NoCurrentYear_ReturnsEmptyFinancialYear()
    {
        var repository = new FakeInvoiceRepository();
        var controller = CreateController(repository, new FakeLookupServiceForInvoice { CurrentYears = [] });

        var result = await controller.GetPending(CancellationToken.None);

        var response = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value;
        var summary = Assert.IsType<PTL.Contracts.Invoice.PendingInvoiceSummaryResponse>(response);
        Assert.Equal(0, summary.FinancialYearId);
        Assert.Equal(string.Empty, summary.FinancialYear);
    }

    [Fact]
    public async Task Generate_ReturnsGenerationResponse()
    {
        var repository = new FakeInvoiceRepository();
        var contractId = Guid.NewGuid();
        repository.PendingData = new InvoicePendingData(
            [new InvoiceContractEntity { ContractId = contractId }],
            [new InvoiceContractItemEntity { ContractId = contractId }]);
        var controller = CreateController(repository);

        var result = await controller.Generate(CancellationToken.None);

        var response = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value;
        var generation = Assert.IsType<PTL.Contracts.Invoice.InvoiceGenerationResponse>(response);
        Assert.True(generation.Success);
        Assert.Equal(1, generation.ContractCount);
    }

    [Fact]
    public async Task Generate_AuthenticatedUser_PassesUserNameAsGeneratedBy()
    {
        var repository = new FakeInvoiceRepository();
        var contractId = Guid.NewGuid();
        repository.PendingData = new InvoicePendingData(
            [new InvoiceContractEntity { ContractId = contractId }],
            [new InvoiceContractItemEntity { ContractId = contractId }]);
        var controller = CreateController(repository);
        var identity = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "alice")], "Test");
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(identity)
            }
        };

        var result = await controller.Generate(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        Assert.Equal("alice", repository.LastAuditWho);
    }

    [Fact]
    public async Task Reset_ReturnsNoContent()
    {
        var repository = new FakeInvoiceRepository();
        var controller = CreateController(repository);

        var result = await controller.Reset(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NoContentResult>(result);
        Assert.True(repository.ResetCalled);
    }

    [Fact]
    public async Task GetAuditHistory_ReturnsMappedRecords()
    {
        var repository = new FakeInvoiceRepository
        {
            AuditHistory = [new InvoiceAuditEntity { AuditInvoiceGenerationId = Guid.NewGuid(), AuditWho = "test.user", AuditDate = DateTime.UtcNow }]
        };
        var controller = CreateController(repository);

        var result = await controller.GetAuditHistory(CancellationToken.None);

        var response = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value;
        var history = Assert.IsType<IReadOnlyList<PTL.Contracts.Invoice.InvoiceAuditRecordResponse>>(response, exactMatch: false);
        Assert.Single(history);
        Assert.Equal("test.user", history[0].AuditWho);
    }

    [Fact]
    public async Task DownloadCsv_ExistingGeneration_ReturnsFileResult()
    {
        var repository = new FakeInvoiceRepository();
        var storage = new FakeInvoiceStorageService();
        var service = new InvoiceService(
            repository,
            storage,
            new FakeNotifyClient(),
            Options.Create(new InvoiceStorageOptions()),
            Options.Create(new InvoiceNotificationOptions { TemplateId = "template-1", Recipients = ["ops@example.com"] }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceService>.Instance);
        var generationId = Guid.NewGuid();
        await storage.SaveAsync($"invoices/{generationId}/PT_Invoices_2026-01-01.csv", [1, 2, 3], "text/csv");
        var controller = new InvoiceController(service, new FakeLookupServiceForInvoice(), Options.Create(new InvoiceNotificationOptions()));

        var result = await controller.DownloadCsv(generationId, CancellationToken.None);

        var fileResult = Assert.IsType<Microsoft.AspNetCore.Mvc.FileContentResult>(result);
        Assert.Equal("text/csv", fileResult.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, fileResult.FileContents);
    }

    [Fact]
    public async Task DownloadCsv_MissingGeneration_ReturnsNotFound()
    {
        var controller = CreateController(new FakeInvoiceRepository());

        var result = await controller.DownloadCsv(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result);
    }
}
