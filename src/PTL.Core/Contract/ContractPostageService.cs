using Microsoft.Extensions.Logging;
using PTL.Core.Lookup;
using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Core.Contract;

public interface IContractPostageService
{
    // Legacy ContractItems getters: for an online order the stored courier / postage / special delivery
    // counts are replaced by a live count each time the items are read. Other contracts are untouched.
    Task ApplyOnlineOrderCountsAsync(ContractItemsAggregate items, CancellationToken cancellationToken = default);

    // Legacy ParticipantScheme.aspx ButtonSave_Click: after a save, an online order's stored counts are
    // recalculated and the contract saved (prices are never touched). No-op for any other contract.
    Task RecalculateAsync(Guid contractId, CancellationToken cancellationToken = default);
}

public sealed class ContractPostageService(
    IContractRepository contractRepository,
    ISchemeRepository schemeRepository,
    ILookupRepository lookupRepository,
    ILogger<ContractPostageService> logger) : IContractPostageService
{
    private static readonly Action<ILogger, Guid, int, int, int, Exception?> LogRecalculatedMessage =
        LoggerMessage.Define<Guid, int, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogRecalculatedMessage)),
            "Recalculated postage for online order {ContractId}: courier={Courier} postage={Postage} specialDelivery={SpecialDelivery}");

    public async Task ApplyOnlineOrderCountsAsync(ContractItemsAggregate items, CancellationToken cancellationToken = default)
    {
        var contract = await contractRepository.GetByIdAsync(items.ContractId, cancellationToken);
        if (contract is not { IsOnlineOrder: true })
        {
            return;
        }

        var counts = await CalculateAsync(items, cancellationToken);
        items.NumberCourier = counts.Courier;
        items.NumberPostage = counts.Postage;
        items.NumberSpecialDelivery = counts.SpecialDelivery;
    }

    public async Task RecalculateAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var contract = await contractRepository.GetByIdAsync(contractId, cancellationToken);
        if (contract is not { IsOnlineOrder: true })
        {
            return;
        }

        var items = await contractRepository.GetContractItemsAsync(contractId, cancellationToken);
        if (items is null)
        {
            return;
        }

        var counts = await CalculateAsync(items, cancellationToken);
        contract.NumberCourier = counts.Courier;
        contract.NumberPostage = counts.Postage;
        contract.NumberSpecialDelivery = counts.SpecialDelivery;
        await contractRepository.UpdateAsync(contract, cancellationToken);
        LogRecalculatedMessage(logger, contractId, counts.Courier, counts.Postage, counts.SpecialDelivery, null);
    }

    private async Task<ContractPostageCounts> CalculateAsync(ContractItemsAggregate items, CancellationToken cancellationToken)
    {
        var schemes = new Dictionary<Guid, CoreScheme>();
        foreach (var schemeId in items.Schemes.Select(s => s.SchemeId).Distinct())
        {
            var scheme = await schemeRepository.GetByIdAsync(schemeId, cancellationToken);
            if (scheme is not null)
            {
                schemes[schemeId] = scheme;
            }
        }

        var plans = new List<PostagePricingPlanEntity>();
        foreach (var yearId in schemes.Values.Select(s => s.YearId).Distinct())
        {
            plans.AddRange(await lookupRepository.GetPostagePricingPlansForYearAsync(yearId, cancellationToken));
        }

        return ContractPostageCalculator.Calculate(items, schemes, plans);
    }
}
