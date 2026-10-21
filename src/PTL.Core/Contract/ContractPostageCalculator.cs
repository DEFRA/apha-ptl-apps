using PTL.Core.Lookup;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Core.Contract;

public sealed record ContractPostageCounts(int Courier, int Postage, int SpecialDelivery);

// Port of legacy ContractItems.CourierPriceTotal / PostagePriceTotal / SpecialDeliveryPriceTotal's
// counting (PtaBusinessObjects/Business Objects/Contracts/ContractItems.vb). Each of the three
// counts a contract's non-removed items whose scheme uses that postage plan: once per selected
// month, except combined-packaging schemes, which share one despatch per month / day of week /
// postage type / week number across the whole contract.
public static class ContractPostageCalculator
{
    // Legacy getPostType names; "Biofreeze" is the plan stored as the contract's Postage.
    public const string CourierPlanName = "Courier";
    public const string PostagePlanName = "Biofreeze";
    public const string SpecialDeliveryPlanName = "Dry Ice";

    public static ContractPostageCounts Calculate(
        ContractItemsAggregate items,
        IReadOnlyDictionary<Guid, CoreScheme> schemes,
        IReadOnlyList<PostagePricingPlanEntity> plans) => new(
            Count(items, schemes, plans, CourierPlanName),
            Count(items, schemes, plans, PostagePlanName),
            Count(items, schemes, plans, SpecialDeliveryPlanName));

    private static int Count(
        ContractItemsAggregate items,
        IReadOnlyDictionary<Guid, CoreScheme> schemes,
        IReadOnlyList<PostagePricingPlanEntity> plans,
        string planName)
    {
        var combined = new HashSet<(int Month, Guid DayOfWeekId, Guid PostageType, int WeekNumber)>();
        var uncombined = 0;

        foreach (var group in items.Schemes)
        {
            var plan = PlanForScheme(group, schemes, plans, planName, out var scheme);
            if (plan is null || scheme is null)
            {
                continue;
            }

            foreach (var month in SelectedMonths(group))
            {
                if (scheme.CombinedPackaging)
                {
                    combined.Add((month, scheme.DayOfWeekId, plan.PostageId, scheme.WeekNumber));
                }
                else
                {
                    uncombined++;
                }
            }
        }

        return combined.Count + uncombined;
    }

    // getPostType(name, realScheme.YearId): the plan of that name for the scheme's own year, when the scheme uses it.
    private static PostagePricingPlanEntity? PlanForScheme(
        ContractItemSchemeGroup group,
        IReadOnlyDictionary<Guid, CoreScheme> schemes,
        IReadOnlyList<PostagePricingPlanEntity> plans,
        string planName,
        out CoreScheme? scheme)
    {
        if (!schemes.TryGetValue(group.SchemeId, out scheme))
        {
            return null;
        }

        var yearId = scheme.YearId;
        var plan = plans.FirstOrDefault(p => string.Equals(p.Name, planName, StringComparison.Ordinal) && p.YearId == yearId);
        return plan is not null && scheme.Postage == plan.PostageId ? plan : null;
    }

    // One entry (1 = January) per ticked month of each non-removed participant line.
    private static IEnumerable<int> SelectedMonths(ContractItemSchemeGroup group) =>
        group.Participants
            .Where(line => !line.IsRemoved)
            .Select(line => line.Months)
            .SelectMany(months => months.Select((ticked, index) => (ticked, month: index + 1)))
            .Where(m => m.ticked)
            .Select(m => m.month);
}
