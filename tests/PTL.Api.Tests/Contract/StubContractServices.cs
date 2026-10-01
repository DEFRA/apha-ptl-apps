using PTL.Core.Contract.Export.Bulk;
using PTL.Core.Contract.Export.Templates;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Contract.Renew;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;

namespace PTL.Api.Tests.Contract;

// ContractController is the single Contract-domain controller, so constructing it needs every
// Contract service. These stubs cover the ones a given test file does not exercise.
internal sealed class StubSampleAddressService : ISampleAddressService
{
    public Task<IReadOnlyList<SampleAddressEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SampleAddressEntity>>([]);
}

internal sealed class StubContractRenewalService : IContractRenewalService
{
    public Task<ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ContractRenewalEntity?>(null);
}

internal sealed class StubRenewContractsService : IRenewContractsService
{
    public Task<RenewableContractsResult> GetRenewableContractsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RenewableContractsResult(new RenewEligibility(true, null), [], []));

    public Task<IReadOnlyList<RenewableContractItemEntity>> GetRenewableItemsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RenewableContractItemEntity>>([]);

    public Task<RenewContractsResult> RenewContractsAsync(
        Guid customerId,
        IReadOnlyList<Guid> contractIds,
        IReadOnlyList<Guid> participantSchemeIds,
        string? newContractSignatory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new RenewContractsResult(true, Guid.NewGuid(), null));
}

internal sealed class StubPendingOrderService : IPendingOrderService
{
    public Task<(IReadOnlyList<PendingOrderSummaryEntity> CurrentYear, IReadOnlyList<PendingOrderSummaryEntity> NextYear)> GetPendingOrdersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<(IReadOnlyList<PendingOrderSummaryEntity>, IReadOnlyList<PendingOrderSummaryEntity>)>(([], []));

    public Task<PendingOrderDetail?> GetPendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PendingOrderDetail?>(null);

    public Task<bool> UpdateSchemeAsync(Guid pendingContractId, Guid pendingParticipantSchemeId, PendingOrderSchemeEdit edit, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> ApprovePendingOrderAsync(Guid pendingContractId, string purchaseOrderNumber, string approvedBy, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> DeclinePendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

internal sealed class StubExportTemplateService : IExportTemplateService
{
    public Task<IReadOnlyList<UploadedTemplate>> GetTemplatesAsync(string documentType, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UploadedTemplate>>([]);

    public Task<UploadedTemplate?> GetActiveTemplateAsync(string documentType, CancellationToken cancellationToken = default) =>
        Task.FromResult<UploadedTemplate?>(null);

    public Task<ExportTemplateUploadResult> UploadAsync(string documentType, string fileName, byte[] content, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExportTemplateUploadResult(false, "File not found", null));

    public Task<ExportTemplateContent?> DownloadAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ExportTemplateContent?>(null);

    public Task<bool> SelectAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> DeleteAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

internal sealed class StubBulkExportService : IBulkExportService
{
    public Task<IReadOnlyList<BulkContractEntity>> GetExportableContractsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BulkContractEntity>>([]);

    public Task<IReadOnlyList<SampleAddressEntity>> GetSampleAddressesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SampleAddressEntity>>([]);

    public Task<IReadOnlyList<ContractRenewalEntity>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ContractRenewalEntity>>([]);
}
