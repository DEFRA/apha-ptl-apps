namespace PTL.Core.TestMethodItemType;

// tblTestMethodItemType - year-versioned reference list consumed as an "Add Test Method Item"
// dropdown option on the Scheme Tests tab (PTL.Core.Lookup.SchemeItemTypeKind.TestMethodItemType
// is the read-only projection used there; this richer shape backs the Test Method Items admin
// screen). Identical shape to TestResultItemType (Slice 4): SharedId propagated across
// split-on-edit rows, a required Return Type (tblTestReturnValueType) foreign key - see
// docs/Parity-Analysis/11-InternalWeb-SchemeItemTypes-Parity-Analysis.md, Slice 5.
public sealed class TestMethodItemType
{
    public Guid TestMethodItemTypeId { get; set; }

    public Guid SharedId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid TestReturnValueTypeId { get; set; }

    public string TestReturnValueTypeName { get; set; } = string.Empty;

    public int StartYearId { get; set; }

    public int EndYearId { get; set; }

    public bool NoLongerInUse { get; set; }

    // dbo.fnCountForeignInstancesOfTestMethodItemType - count of Schemes referencing this item
    // type; -1 means it is also used on a Distribution. Non-zero blocks Edit/Remove - see
    // TestMethodItemTypeService.
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;
}
