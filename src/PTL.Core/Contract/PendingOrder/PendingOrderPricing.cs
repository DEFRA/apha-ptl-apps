using PTL.Core.Lookup;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Core.Contract.PendingOrder;

// Direct port of PendingContractOrder.aspx.vb's pricing helpers (getTotalSchemePrice,
// getPostTypePrice, getPostageTypeNumbers, getTotalPostagePrice, GetPostagePrice). Kept as a pure
// static class so the arithmetic is unit-testable without a database.
public static class PendingOrderPricing
{
    // Legacy getTotalPostagePrice looks these three plans up by name for the order's year.
    private static readonly string[] PostagePlanNames = ["Courier", "Biofreeze", "Dry Ice"];

    // Legacy getTotalSchemePrice - removed lines are excluded.
    public static decimal TotalSchemePrice(IEnumerable<PendingOrderSchemeEntity> schemes) =>
        schemes.Where(s => !s.IsRemoved).Sum(s => s.Price);

    // Legacy PendingParticipantScheme.GetPostagePrice - the scheme's postage plan price for the
    // customer's country type, charged once per selected distribution month.
    public static decimal SchemePostagePrice(
        PendingOrderSchemeEntity scheme,
        CoreScheme? realScheme,
        IReadOnlyList<PostagePricingPlanEntity> postagePlans,
        string countryType)
    {
        if (realScheme is null)
        {
            return 0m;
        }

        var plan = postagePlans.FirstOrDefault(p => p.PostageId == realScheme.Postage);
        if (plan is null)
        {
            return 0m;
        }

        var monthsSelected = scheme.Months().Count(m => m.Selected);
        return PriceForCountryType(plan, countryType) * monthsSelected;
    }

    // Legacy getTotalPostagePrice: for each of the three named plans, unit price for the customer's
    // country type multiplied by the plan's chargeable distribution count.
    public static decimal TotalPostagePrice(
        IEnumerable<PendingOrderSchemeEntity> schemes,
        IReadOnlyDictionary<Guid, CoreScheme> realSchemes,
        IReadOnlyList<PostagePricingPlanEntity> postagePlans,
        string countryType)
    {
        var schemeList = schemes.ToList();
        var total = 0m;

        foreach (var planName in PostagePlanNames)
        {
            var plan = postagePlans.FirstOrDefault(p => string.Equals(p.Name, planName, StringComparison.OrdinalIgnoreCase));
            if (plan is null)
            {
                continue;
            }

            total += PriceForCountryType(plan, countryType) * PostageTypeNumbers(schemeList, realSchemes, plan.PostageId);
        }

        return total;
    }

    // Legacy getPostageTypeNumbers. Combined-packaging schemes share a despatch when they fall in
    // the same month, on the same day of week, in the same week number, for the same postage type -
    // so those are counted once per distinct combination. Everything else is counted per month.
    public static int PostageTypeNumbers(
        IEnumerable<PendingOrderSchemeEntity> schemes,
        IReadOnlyDictionary<Guid, CoreScheme> realSchemes,
        Guid postageType)
    {
        var combined = new HashSet<(int Month, Guid DayOfWeekId, Guid PostageType, int WeekNumber)>();
        var nonCombinedTotal = 0;

        foreach (var scheme in schemes.Where(s => !s.IsRemoved))
        {
            if (!realSchemes.TryGetValue(scheme.SchemeId, out var realScheme) || realScheme.Postage != postageType)
            {
                continue;
            }

            foreach (var month in scheme.Months().Where(m => m.Selected))
            {
                if (realScheme.CombinedPackaging)
                {
                    combined.Add((month.MonthNumber, realScheme.DayOfWeekId, realScheme.Postage.GetValueOrDefault(), realScheme.WeekNumber));
                }
                else
                {
                    nonCombinedTotal++;
                }
            }
        }

        return combined.Count + nonCombinedTotal;
    }

    // Legacy getCountryType - tblCountryType's name, used verbatim ("UK" / "EU" / anything else is
    // treated as non-EU).
    public static string CountryTypeFor(IReadOnlyList<CountryEntity> countries, Guid countryId) =>
        countries.FirstOrDefault(c => c.CountryId == countryId)?.CountryType ?? string.Empty;

    private static decimal PriceForCountryType(PostagePricingPlanEntity plan, string countryType) => countryType switch
    {
        "UK" => plan.UKPrice.GetValueOrDefault(),
        "EU" => plan.EUPrice.GetValueOrDefault(),
        _ => plan.NonEUPrice.GetValueOrDefault()
    };
}
