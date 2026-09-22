namespace PTL.Contracts.Lookup;

// Public API contract for postage pricing plan reads (spgPostageByYearID) - see
// docs/analysis/scheme-analysis.md, "Postage Pricing Plan Read Operations".
public sealed record PostagePricingPlanResponse(Guid PostageId, string Name, decimal? UKPrice, decimal? EUPrice, decimal? NonEUPrice, int YearId);
