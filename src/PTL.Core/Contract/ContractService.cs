using Microsoft.Extensions.Logging;
using PTL.Contracts.Contract;

namespace PTL.Core.Contract;

public sealed class ContractService(IContractRepository contractRepository, ILogger<ContractService> logger) : IContractService
{
    private static readonly Action<ILogger, Guid, int?, string?, int, int, Exception?> LogContractSearchMessage =
        LoggerMessage.Define<Guid, int?, string?, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogContractSearchMessage)),
            "Contract search: customerId={CustomerId} yearId={YearId} searchTerm={SearchTerm} page={Page} totalResults={TotalCount}");

    private static readonly Action<ILogger, Guid, int, string, Exception?> LogCreatedContractMessage =
        LoggerMessage.Define<Guid, int, string>(
            LogLevel.Information,
            new EventId(2, nameof(LogCreatedContractMessage)),
            "Created contract {ContractId} for year {YearId} ({Suffix})");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdateUnknownContractMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(3, nameof(LogUpdateUnknownContractMessage)),
            "Update requested for unknown contract {ContractId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedContractMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, nameof(LogUpdatedContractMessage)),
            "Updated contract {ContractId}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogContractValidationFailedMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(5, nameof(LogContractValidationFailedMessage)),
            "Contract validation failed for {ContractId}: {Errors}");

    public Task<Contract?> GetContractAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        contractRepository.GetByIdAsync(contractId, cancellationToken);

    public async Task<ContractSearchResult> SearchContractsAsync(Guid customerId, int? yearId, ContractPeriodFilter period, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        // Exact-year lookup mirrors ContractList.aspx's year-filtered variant - no search/paging
        // applied, matching legacy behaviour (spgContractInfoByCustomerIdAndYearId has no such concept).
        if (yearId.HasValue)
        {
            var byYear = await contractRepository.GetSummariesByYearAsync(customerId, yearId.Value, cancellationToken);
            LogContractSearchMessage(logger, customerId, yearId, searchTerm, page, byYear.Count, null);
            return new ContractSearchResult(byYear, byYear.Count);
        }

        var all = await contractRepository.GetSummariesAsync(customerId, period, cancellationToken);

        // Suffix is the only free-text field on the ContractInfo projection (see
        // docs/analysis/contract-analysis.md) - search is therefore scoped to it.
        var filtered = string.IsNullOrWhiteSpace(searchTerm)
            ? all
            : all.Where(c => c.Suffix.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        var totalCount = filtered.Count;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        LogContractSearchMessage(logger, customerId, yearId, searchTerm, page, totalCount, null);

        return new ContractSearchResult(items, totalCount);
    }

    public async Task<Contract> CreateContractAsync(Contract contract, CancellationToken cancellationToken = default)
    {
        // ContractId and CommencementDate are always server-generated, matching the legacy
        // Contract.DataPortal_Create() behaviour - never trust client-supplied values here.
        contract.ContractId = Guid.NewGuid();
        contract.CommencementDate = DateTime.UtcNow;

        // [NEEDS INVESTIGATION] Legacy Contract.DataPortal_Create() defaults AdministrationCharge
        // from the customer's currency via SystemObjects.AdministrationChargeCurrencyCollection,
        // which has not been ported to this codebase (no corresponding repository/stored procedure
        // integration exists yet). Callers must supply AdministrationCharge explicitly for now.
        Validate(contract);

        var created = await contractRepository.CreateAsync(contract, cancellationToken);
        LogCreatedContractMessage(logger, created.ContractId, created.YearId, created.Suffix, null);
        return created;
    }

    public async Task<Contract?> UpdateContractAsync(Guid contractId, Contract updatedFields, CancellationToken cancellationToken = default)
    {
        var existing = await contractRepository.GetByIdAsync(contractId, cancellationToken);
        if (existing is null)
        {
            LogUpdateUnknownContractMessage(logger, contractId, null);
            return null;
        }

        if (existing.IsReadOnly)
        {
            // Preserves the legacy Contract.aspx.vb SetReadOnly() intent (enforced client-side only
            // in Web Forms) as an explicit server-side rule: contracts belonging to a closed year
            // (YearId < current year) cannot be edited.
            throw new ContractValidationException([new ContractValidationError(string.Empty, "This contract belongs to a closed year and cannot be edited.")]);
        }

        // CustomerId, CommencementDate, IsInvoiceSent, ApprovedBy, and ApprovedDate are immutable
        // via this API - Contract.aspx never posts them back, and spuContract's
        // "CASE @ApprovedBy WHEN NULL THEN ... ELSE @ApprovedBy END" always evaluates to @ApprovedBy
        // (a SQL NULL-comparison quirk: WHEN NULL never matches), so passing null would silently wipe
        // ApprovedBy/ApprovedDate on every update unless explicitly preserved here.
        updatedFields.ContractId = existing.ContractId;
        updatedFields.CustomerId = existing.CustomerId;
        updatedFields.CommencementDate = existing.CommencementDate;
        updatedFields.IsInvoiceSent = existing.IsInvoiceSent;
        updatedFields.ApprovedBy = existing.ApprovedBy;
        updatedFields.ApprovedDate = existing.ApprovedDate;

        Validate(updatedFields);

        var updated = await contractRepository.UpdateAsync(updatedFields, cancellationToken);
        LogUpdatedContractMessage(logger, contractId, null);
        return updated;
    }

    private void Validate(Contract contract)
    {
        var result = ContractValidator.Validate(contract);
        if (!result.IsValid)
        {
            LogContractValidationFailedMessage(logger, contract.ContractId, string.Join("; ", result.Errors.Select(e => e.Message)), null);
            throw new ContractValidationException(result.Errors);
        }
    }
}
