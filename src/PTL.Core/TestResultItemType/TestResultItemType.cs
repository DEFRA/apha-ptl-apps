using PTL.Core.ReturnTypeItems;

namespace PTL.Core.TestResultItemType;

// tblTestResultItemType - year-versioned reference list consumed as an "Add Test Result Item"
// dropdown option on the Scheme Tests tab (PTL.Core.Lookup.SchemeItemTypeKind.TestResultItemType
// is the read-only projection used there; this richer shape backs the Test Result Items admin
// screen). Like Test Types, every split-on-edit row carries a shared SharedId linking "the same
// conceptual item" across years. Unlike Test Types, each item also carries a required Return Type
// (tblTestReturnValueType) foreign key - see
// docs/Parity-Analysis/11-InternalWeb-SchemeItemTypes-Parity-Analysis.md, Slice 4.
public sealed class TestResultItemType : ISplitOnEditItem<TestResultItemType>
{
    public Guid TestResultItemTypeId { get; set; }

    public Guid SharedId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid TestReturnValueTypeId { get; set; }

    public string TestReturnValueTypeName { get; set; } = string.Empty;

    public int StartYearId { get; set; }

    public int EndYearId { get; set; }

    public bool NoLongerInUse { get; set; }

    // dbo.fnCountForeignInstancesOfTestResultItemType - count of Schemes referencing this item
    // type; -1 means it is also used on a Distribution. Non-zero blocks Edit/Remove - see
    // TestResultItemTypeService.
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;

    public static TestResultItemType CreateSplit(Guid sharedId, string name, Guid testReturnValueTypeId, string testReturnValueTypeName, int startYearId, int endYearId, bool noLongerInUse) =>
        new()
        {
            TestResultItemTypeId = Guid.NewGuid(),
            SharedId = sharedId,
            Name = name,
            TestReturnValueTypeId = testReturnValueTypeId,
            TestReturnValueTypeName = testReturnValueTypeName,
            StartYearId = startYearId,
            EndYearId = endYearId,
            NoLongerInUse = noLongerInUse
        };
}

