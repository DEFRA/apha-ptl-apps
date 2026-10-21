namespace PTL.Core.TestType;

// tblTestType - year-versioned reference list consumed as a "Add Test" dropdown option on the
// Scheme Tests tab (PTL.Core.Lookup.SchemeItemTypeKind.TestType is the read-only projection used
// there; this richer shape backs the Test Types admin screen). Unlike Category/Criterion Items,
// every split-on-edit row carries a shared SharedId linking "the same conceptual item" across
// years - see docs/Parity-Analysis/11-InternalWeb-SchemeItemTypes-Parity-Analysis.md §11.3.
public sealed class TestType
{
    public Guid TestTypeId { get; set; }

    public Guid SharedId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int StartYearId { get; set; }

    public int EndYearId { get; set; }

    public bool NoLongerInUse { get; set; }

    // dbo.fnCountForeignInstancesOfTestType - count of Schemes referencing this item type; -1
    // means it is also used on a Distribution. Non-zero blocks Edit/Remove - see TestTypeService.
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;
}
