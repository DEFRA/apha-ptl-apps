using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PTL.Core.Invoice;

namespace PTL.Api.Tests.Invoice;

public class InvoiceServiceTests
{
    private static (InvoiceService Service, FakeInvoiceRepository Repository, FakeInvoiceStorageService Storage, FakeNotifyClient Notify) CreateService(
        IReadOnlyList<string>? recipients = null,
        string templateId = "template-1")
    {
        var repository = new FakeInvoiceRepository();
        var storage = new FakeInvoiceStorageService();
        var notify = new FakeNotifyClient();
        var service = new InvoiceService(
            repository,
            storage,
            notify,
            Options.Create(new InvoiceStorageOptions { Prefix = "invoices" }),
            Options.Create(new InvoiceNotificationOptions { TemplateId = templateId, Recipients = recipients ?? ["ops@example.com"] }),
            NullLogger<InvoiceService>.Instance);

        return (service, repository, storage, notify);
    }

    private static InvoiceContractEntity Contract(Guid contractId, bool optOut = false) => new()
    {
        ContractId = contractId,
        QalNumber = "QAL001",
        Suffix = "A",
        InvoiceOrganisation = "Test Org",
        CustomerNumber = "CUST1",
        CustomerType = "Business",
        OptOutOfInvoiceGeneration = optOut
    };

    private static InvoiceContractItemEntity Item(Guid contractId, bool nonFeePaying = false) => new()
    {
        ContractId = contractId,
        SchemeIdentifier = "PT0001",
        SchemeName = "Scheme One",
        Price = 100m,
        NonFeePaying = nonFeePaying
    };

    [Fact]
    public async Task GetPendingSummaryAsync_ComputesInformationalCountsOnly()
    {
        var (service, repository, _, _) = CreateService();
        var optOutContractId = Guid.NewGuid();
        var normalContractId = Guid.NewGuid();
        repository.PendingData = new InvoicePendingData(
            [Contract(optOutContractId, optOut: true), Contract(normalContractId)],
            [Item(optOutContractId, nonFeePaying: true), Item(normalContractId)]);

        var summary = await service.GetPendingSummaryAsync();

        Assert.Equal(2, summary.EligibleContractCount);
        Assert.Equal(1, summary.OptOutContractCount);
        Assert.Equal(1, summary.NonFeePayingItemCount);
    }

    [Fact]
    public async Task GenerateInvoicesAsync_Success_SavesCsvMarksInvoicedAndNotifiesRecipients()
    {
        var (service, repository, storage, notify) = CreateService(recipients: ["ops@example.com", "finance@example.com"]);
        var contractId = Guid.NewGuid();
        repository.PendingData = new InvoicePendingData([Contract(contractId)], [Item(contractId)]);

        var outcome = await service.GenerateInvoicesAsync("test.user");

        Assert.True(outcome.Success);
        Assert.Equal(1, outcome.ContractCount);
        Assert.NotNull(outcome.CsvStorageKey);
        Assert.StartsWith("invoices/PT_Invoices_", outcome.CsvStorageKey);
        Assert.True(repository.MarkInvoicedCalled);
        Assert.Equal("test.user", repository.LastAuditWho);
        Assert.Equal(storage.LastStorageKey, outcome.CsvStorageKey);
        Assert.NotNull(storage.LastContent);
        Assert.Equal(["ops@example.com", "finance@example.com"], notify.SentTo);
    }

    [Fact]
    public async Task GenerateInvoicesAsync_NoEligibleContracts_StillSucceedsWithEmptyCsv()
    {
        var (service, repository, storage, _) = CreateService();
        repository.PendingData = new InvoicePendingData([], []);

        var outcome = await service.GenerateInvoicesAsync("test.user");

        Assert.True(outcome.Success);
        Assert.Equal(0, outcome.ContractCount);
        Assert.True(repository.MarkInvoicedCalled);
        Assert.NotNull(storage.LastContent);
    }

    [Fact]
    public async Task GenerateInvoicesAsync_NotificationFailure_DoesNotRollBackGeneration()
    {
        var (service, repository, _, notify) = CreateService(recipients: ["bad@example.com", "good@example.com"]);
        notify.ThrowForEmailAddress = "bad@example.com";
        var contractId = Guid.NewGuid();
        repository.PendingData = new InvoicePendingData([Contract(contractId)], [Item(contractId)]);

        var outcome = await service.GenerateInvoicesAsync("test.user");

        Assert.True(outcome.Success);
        Assert.True(repository.MarkInvoicedCalled);
        Assert.Equal(["good@example.com"], notify.SentTo);
    }

    [Fact]
    public async Task GenerateInvoicesAsync_RepositoryThrows_ReturnsFailureWithLegacyErrorText()
    {
        var (service, repository, _, _) = CreateService();
        repository.ThrowOnGetPendingData = true;

        var outcome = await service.GenerateInvoicesAsync("test.user");

        Assert.False(outcome.Success);
        Assert.Equal(0, outcome.ContractCount);
        Assert.Null(outcome.CsvStorageKey);
        Assert.Equal("Invoices could not be generated. Please try again later.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task GenerateInvoicesAsync_StorageThrows_ReturnsFailureAndDoesNotMarkInvoiced()
    {
        var (service, repository, storage, _) = CreateService();
        storage.ThrowOnSave = true;
        var contractId = Guid.NewGuid();
        repository.PendingData = new InvoicePendingData([Contract(contractId)], [Item(contractId)]);

        var outcome = await service.GenerateInvoicesAsync("test.user");

        Assert.False(outcome.Success);
        Assert.False(repository.MarkInvoicedCalled);
    }

    [Fact]
    public async Task ResetInvoicedFlagsAsync_DelegatesToRepository()
    {
        var (service, repository, _, _) = CreateService();

        await service.ResetInvoicedFlagsAsync();

        Assert.True(repository.ResetCalled);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_DelegatesToRepository()
    {
        var (service, repository, _, _) = CreateService();
        repository.AuditHistory = [new InvoiceAuditEntity { AuditWho = "test.user", AuditDate = DateTime.UtcNow }];

        var history = await service.GetAuditHistoryAsync();

        Assert.Single(history);
        Assert.Equal("test.user", history[0].AuditWho);
    }

    [Fact]
    public async Task GenerateInvoicesAsync_NoRecipientsConfigured_SkipsNotificationButStillSucceeds()
    {
        var (service, repository, _, notify) = CreateService(recipients: []);
        var contractId = Guid.NewGuid();
        repository.PendingData = new InvoicePendingData([Contract(contractId)], [Item(contractId)]);

        var outcome = await service.GenerateInvoicesAsync("test.user");

        Assert.True(outcome.Success);
        Assert.Empty(notify.SentTo);
    }
}
