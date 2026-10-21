namespace PTL.Core.Lookup;

// Keyless single-row projection for spgaSystemSettings (tblSystemSettings). Only UTNumber is
// modelled here - it is the one field the Contract domain needs (legacy Contract.DataPortal_Create
// defaults a new contract's UTNumber from SystemSettings.FetchSystemSettings().UTNumber, see
// docs/analysis/contract-analysis.md, "Default pricing derivation on creation").
public class SystemSettingsEntity
{
    public string UTNumber { get; set; } = string.Empty;

    // dbo.fnGetNextYearWithDelayId() - the year a renewed contract belongs to, and the threshold
    // legacy ContractMergeService.IsMergeAllowed compares every existing contract's year against.
    public int NextYearWithDelayId { get; set; }

    // dbo.fnGetCurrentYearId() / fnGetNextYearId() - the two contract years Review Pending Orders
    // splits its grids by. NOT interchangeable with NextYearWithDelayId.
    public int CurrentYearId { get; set; }

    public int NextYearId { get; set; }

    // tblSystemSettings.fldContractStartDate - the month of this date decides whether a scheme's
    // derived start date falls in its own year or the next one (legacy Scheme.RecalucalateStartDate).
    public DateTime ContractStartDate { get; set; }

    // Hardcoded 9999 in spgaSystemSettings - the "open-ended" sentinel legacy's year-versioned
    // reference lists (Category/Criterion/Test Method/Test Result Item Types, Test Types) use as
    // EndYearId for a row with no known end date (legacy CategoryItemType.DataPortal_Create).
    public int FinalYearId { get; set; }
}
