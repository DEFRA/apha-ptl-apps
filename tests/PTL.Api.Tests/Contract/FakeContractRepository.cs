using PTL.Contracts.Contract;
using PTL.Core.Contract;

namespace PTL.Api.Tests.Contract;

// In-memory IContractRepository test double so ContractService can be tested without a real
// database or the spgContract*/spiContract/spuContract stored procedures.
internal sealed class FakeContractRepository : IContractRepository
{
    private readonly Dictionary<Guid, PTL.Core.Contract.Contract> _contracts = [];

    public Task<PTL.Core.Contract.Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_contracts.TryGetValue(contractId, out var contract) ? Clone(contract) : null);

    public Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesAsync(Guid customerId, ContractPeriodFilter period, CancellationToken cancellationToken = default)
    {
        var currentYear = DateTime.UtcNow.Year;
        var filtered = _contracts.Values.Where(c => c.CustomerId == customerId && period switch
        {
            ContractPeriodFilter.Historical => c.YearId < currentYear,
            ContractPeriodFilter.CurrentAndNext => c.YearId >= currentYear,
            _ => true
        });

        IReadOnlyList<ContractSummaryEntity> summaries = filtered
            .Select(ToSummary)
            .ToList();

        return Task.FromResult(summaries);
    }

    public Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ContractSummaryEntity> summaries = _contracts.Values
            .Where(c => c.CustomerId == customerId && c.YearId == yearId)
            .Select(ToSummary)
            .ToList();

        return Task.FromResult(summaries);
    }

    public Task<PTL.Core.Contract.Contract> CreateAsync(PTL.Core.Contract.Contract contract, CancellationToken cancellationToken = default)
    {
        contract.QalNumber = "QAL/00001";
        contract.CustomerName = "Sample Laboratories Ltd";
        contract.IsReadOnly = contract.YearId < DateTime.UtcNow.Year;
        _contracts[contract.ContractId] = Clone(contract);
        return Task.FromResult(Clone(contract));
    }

    public Task<PTL.Core.Contract.Contract?> UpdateAsync(PTL.Core.Contract.Contract contract, CancellationToken cancellationToken = default)
    {
        if (!_contracts.TryGetValue(contract.ContractId, out var existing))
        {
            return Task.FromResult<PTL.Core.Contract.Contract?>(null);
        }

        contract.QalNumber = existing.QalNumber;
        contract.CustomerName = existing.CustomerName;
        contract.IsReadOnly = contract.YearId < DateTime.UtcNow.Year;
        _contracts[contract.ContractId] = Clone(contract);
        return Task.FromResult<PTL.Core.Contract.Contract?>(Clone(contract));
    }

    private static ContractSummaryEntity ToSummary(PTL.Core.Contract.Contract c) => new()
    {
        ContractId = c.ContractId,
        CustomerId = c.CustomerId,
        YearId = c.YearId,
        IsActive = c.IsActive,
        Suffix = c.Suffix
    };

    private static PTL.Core.Contract.Contract Clone(PTL.Core.Contract.Contract source) => new()
    {
        ContractId = source.ContractId,
        CustomerId = source.CustomerId,
        CustomerName = source.CustomerName,
        QalNumber = source.QalNumber,
        YearId = source.YearId,
        UTNumber = source.UTNumber,
        FTNumber = source.FTNumber,
        ContractSignatory = source.ContractSignatory,
        ActionsRequired = source.ActionsRequired,
        RenewalInformation = source.RenewalInformation,
        DiscountRate = source.DiscountRate,
        AdministrationCharge = source.AdministrationCharge,
        NumberCourier = source.NumberCourier,
        CourierPrice = source.CourierPrice,
        NumberPostage = source.NumberPostage,
        PostagePrice = source.PostagePrice,
        NumberSpecialDelivery = source.NumberSpecialDelivery,
        SpecialDeliveryPrice = source.SpecialDeliveryPrice,
        AcknowledgementPostedDate = source.AcknowledgementPostedDate,
        AcknowledgementReturnedDate = source.AcknowledgementReturnedDate,
        JobSheetPostedDate = source.JobSheetPostedDate,
        ReasonForClosure = source.ReasonForClosure,
        DateOfLeaving = source.DateOfLeaving,
        IsActive = source.IsActive,
        IsReadOnly = source.IsReadOnly,
        Suffix = source.Suffix,
        CommencementDate = source.CommencementDate,
        PurchaseOrderNumber = source.PurchaseOrderNumber,
        OptOutOfInvoiceGeneration = source.OptOutOfInvoiceGeneration,
        IsInvoiceSent = source.IsInvoiceSent,
        IsOnlineOrder = source.IsOnlineOrder,
        ApprovedBy = source.ApprovedBy,
        ApprovedDate = source.ApprovedDate
    };
}
