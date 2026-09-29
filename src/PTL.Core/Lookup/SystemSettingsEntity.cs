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
}
