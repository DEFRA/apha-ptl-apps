namespace PTL.Contracts.Lookup;

// GET /api/lookups/system-settings - UTNumber is the suggested UT number default on the Contract
// Create screen (see docs/analysis/contract-analysis.md); ContractStartDate and the two year ids
// drive the Scheme screen's derived Start Date and its default year.
public sealed record SystemSettingsResponse(
    string UTNumber,
    DateTime ContractStartDate = default,
    int CurrentYearId = 0,
    int NextYearId = 0);
