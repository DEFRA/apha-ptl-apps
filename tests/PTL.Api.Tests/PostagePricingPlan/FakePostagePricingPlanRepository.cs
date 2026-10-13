using PTL.Core.Lookup;
using PTL.Core.PostagePricingPlan;

namespace PTL.Api.Tests.PostagePricingPlan;

// In-memory IPostagePricingPlanRepository test double so PostagePricingPlanService can be tested
// without a real database or the spgaPostagePricingYears/spuPostage/sppRenewPostagePricingPlan
// stored procedures.
internal sealed class FakePostagePricingPlanRepository : IPostagePricingPlanRepository
{
    public IReadOnlyList<YearEntity> YearsWithPlan { get; set; } = [];
    public Dictionary<Guid, PostagePricingPlanEntity> ExistingById { get; set; } = [];
    public List<(Guid PostageId, int YearId, decimal UKPrice, decimal EUPrice, decimal NonEUPrice)> SavedPrices { get; } = [];
    public List<int> RenewedYearIds { get; } = [];

    public Task<IReadOnlyList<YearEntity>> GetYearsWithPlanAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(YearsWithPlan);

    public Task<PostagePricingPlanEntity?> GetByIdAsync(Guid postageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ExistingById.GetValueOrDefault(postageId));

    public Task SetPriceAsync(Guid postageId, int yearId, decimal ukPrice, decimal euPrice, decimal nonEuPrice, CancellationToken cancellationToken = default)
    {
        SavedPrices.Add((postageId, yearId, ukPrice, euPrice, nonEuPrice));
        return Task.CompletedTask;
    }

    public Task RenewAsync(int newYearId, CancellationToken cancellationToken = default)
    {
        RenewedYearIds.Add(newYearId);
        YearsWithPlan = [.. YearsWithPlan, new YearEntity { YearId = newYearId, Year = $"{newYearId}/{(newYearId + 1) % 100:00}" }];
        return Task.CompletedTask;
    }
}
