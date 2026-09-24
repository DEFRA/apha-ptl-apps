namespace PTL.Contracts.Lookup;

// GET /api/lookups/system-settings - only the field the Contract Create screen needs
// (the suggested UT number default, see docs/analysis/contract-analysis.md).
public sealed record SystemSettingsResponse(string UTNumber);
