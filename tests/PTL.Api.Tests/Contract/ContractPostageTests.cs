using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Scheme;
using PTL.Core.Contract;
using PTL.Core.Lookup;
using CoreContract = PTL.Core.Contract.Contract;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Contract;

public sealed class ContractPostageTests
{
    private const int Year = 2026;
    private static readonly Guid CourierPlan = Guid.NewGuid();
    private static readonly Guid BiofreezePlan = Guid.NewGuid();
    private static readonly Guid DryIcePlan = Guid.NewGuid();
    private static readonly Guid Monday = Guid.NewGuid();

    private static readonly PostagePricingPlanEntity[] Plans =
    [
        new() { PostageId = CourierPlan, Name = "Courier", YearId = Year },
        new() { PostageId = BiofreezePlan, Name = "Biofreeze", YearId = Year },
        new() { PostageId = DryIcePlan, Name = "Dry Ice", YearId = Year }
    ];

    private static CoreScheme Scheme(Guid? postage, bool combined = false, int week = 1, Guid? dayOfWeek = null, int yearId = Year) => new()
    {
        SchemeId = Guid.NewGuid(),
        YearId = yearId,
        Postage = postage,
        CombinedPackaging = combined,
        WeekNumber = week,
        DayOfWeekId = dayOfWeek ?? Monday
    };

    private static ContractItemSchemeGroup Group(CoreScheme scheme, params int[][] participantMonths) => new()
    {
        SchemeId = scheme.SchemeId,
        Participants = [.. participantMonths.Select(months => new ContractItemLine
        {
            Months = [.. Enumerable.Range(1, 12).Select(m => months.Contains(m))]
        })]
    };

    private static ContractPostageCounts Calculate(ContractItemsAggregate items, params CoreScheme[] schemes) =>
        ContractPostageCalculator.Calculate(items, schemes.ToDictionary(s => s.SchemeId), Plans);

    [Fact]
    public void UncombinedScheme_CountsOncePerSelectedMonthPerParticipant()
    {
        var scheme = Scheme(CourierPlan);
        var items = new ContractItemsAggregate { Schemes = [Group(scheme, [1, 2, 3], [1])] };

        var counts = Calculate(items, scheme);

        Assert.Equal(new ContractPostageCounts(4, 0, 0), counts);
    }

    [Fact]
    public void CombinedScheme_CountsOnePerDistinctMonth_AcrossParticipants()
    {
        var scheme = Scheme(CourierPlan, combined: true);
        var items = new ContractItemsAggregate { Schemes = [Group(scheme, [1, 2], [2, 3], [3])] };

        Assert.Equal(3, Calculate(items, scheme).Courier);
    }

    [Fact]
    public void CombinedSchemes_ShareADespatchOnlyOnTheSameDayAndWeek()
    {
        var a = Scheme(CourierPlan, combined: true, week: 1);
        var sameSlot = Scheme(CourierPlan, combined: true, week: 1);
        var otherWeek = Scheme(CourierPlan, combined: true, week: 2);
        var otherDay = Scheme(CourierPlan, combined: true, week: 1, dayOfWeek: Guid.NewGuid());
        var items = new ContractItemsAggregate { Schemes = [Group(a, [5]), Group(sameSlot, [5]), Group(otherWeek, [5]), Group(otherDay, [5])] };

        Assert.Equal(3, Calculate(items, a, sameSlot, otherWeek, otherDay).Courier);
    }

    [Fact]
    public void EachPostageType_IsCountedSeparately()
    {
        var courier = Scheme(CourierPlan);
        var biofreeze = Scheme(BiofreezePlan);
        var dryIce = Scheme(DryIcePlan);
        var none = Scheme(null);
        var items = new ContractItemsAggregate { Schemes = [Group(courier, [1]), Group(biofreeze, [1, 2]), Group(dryIce, [1, 2, 3]), Group(none, [1, 2, 3, 4])] };

        Assert.Equal(new ContractPostageCounts(1, 2, 3), Calculate(items, courier, biofreeze, dryIce, none));
    }

    [Fact]
    public void PlanFromAnotherYear_DoesNotMatch()
    {
        var scheme = Scheme(CourierPlan, yearId: Year + 1);
        var items = new ContractItemsAggregate { Schemes = [Group(scheme, [1])] };

        Assert.Equal(0, Calculate(items, scheme).Courier);
    }

    private static async Task<(ContractPostageService Service, FakeContractRepository Contracts, ContractItemsAggregate Items, Guid ContractId)> CreateService(bool isOnlineOrder)
    {
        var contractId = Guid.NewGuid();
        var contracts = new FakeContractRepository();
        contracts.Seed(new CoreContract { ContractId = contractId, CustomerId = Guid.NewGuid(), YearId = Year, IsOnlineOrder = isOnlineOrder, NumberCourier = 99, NumberPostage = 99, NumberSpecialDelivery = 99 });

        var schemes = new FakeSchemeRepository();
        var scheme = Scheme(CourierPlan);
        await schemes.CreateAsync(scheme);

        var items = new ContractItemsAggregate { ContractId = contractId, Schemes = [Group(scheme, [1, 2])] };
        contracts.ContractItems = items;

        var service = new ContractPostageService(contracts, schemes, new FakeLookupRepository { PostagePricingPlans = Plans }, NullLogger<ContractPostageService>.Instance);
        return (service, contracts, items, contractId);
    }

    [Fact]
    public async Task ApplyOnlineOrderCounts_OnlineOrder_ReplacesTheStoredCountsWithALiveCount()
    {
        var (service, _, items, _) = await CreateService(isOnlineOrder: true);
        items.NumberCourier = 99;

        await service.ApplyOnlineOrderCountsAsync(items);

        Assert.Equal(2, items.NumberCourier);
        Assert.Equal(0, items.NumberPostage);
        Assert.Equal(0, items.NumberSpecialDelivery);
    }

    [Fact]
    public async Task ApplyOnlineOrderCounts_OtherContract_KeepsTheStoredCounts()
    {
        var (service, _, items, _) = await CreateService(isOnlineOrder: false);
        items.NumberCourier = 99;

        await service.ApplyOnlineOrderCountsAsync(items);

        Assert.Equal(99, items.NumberCourier);
    }

    [Fact]
    public async Task Recalculate_OnlineOrder_SavesTheNewCountsOnTheContract()
    {
        var (service, contracts, _, contractId) = await CreateService(isOnlineOrder: true);

        await service.RecalculateAsync(contractId);

        var saved = contracts.Contracts[contractId];
        Assert.Equal((2, 0, 0), (saved.NumberCourier, saved.NumberPostage, saved.NumberSpecialDelivery));
    }

    [Fact]
    public async Task Recalculate_OtherContract_LeavesTheContractAlone()
    {
        var (service, contracts, _, contractId) = await CreateService(isOnlineOrder: false);

        await service.RecalculateAsync(contractId);

        Assert.Equal(99, contracts.Contracts[contractId].NumberCourier);
    }

    [Fact]
    public async Task Recalculate_UnknownContract_DoesNothing()
    {
        var (service, _, _, _) = await CreateService(isOnlineOrder: true);

        await service.RecalculateAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task ContractService_GetContractItems_AppliesTheOnlineOrderCounts()
    {
        var contracts = new FakeContractRepository { ContractItems = new ContractItemsAggregate { ContractId = Guid.NewGuid() } };
        var postage = new FakeContractPostageService { Counts = new ContractPostageCounts(4, 5, 6) };
        var service = new ContractService(contracts, new FakeParticipantSchemeRepository(), postage, new FakeAuditActorResolver(), NullLogger<ContractService>.Instance);

        var items = await service.GetContractItemsAsync(contracts.ContractItems.ContractId);

        Assert.Equal((4, 5, 6), (items!.NumberCourier, items.NumberPostage, items.NumberSpecialDelivery));
    }
}
