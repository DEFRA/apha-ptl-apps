namespace PTL.Core.CriterionItemType;

// tblCriterionItemType - year-versioned reference list consumed as an "Add Criterion" dropdown
// option on the Scheme Tests tab (PTL.Core.Lookup.SchemeItemTypeKind.CriterionItemType is the
// read-only projection used there; this richer shape backs the Criteria Items admin screen). No
// SharedId - confirmed by reading spiCriterionItemType.sql/spuCriterionItemType.sql, neither of
// which has a @SharedId parameter or fldSharedId column, unlike Test Types.
public sealed class CriterionItemType
{
    public Guid CriterionItemTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int StartYearId { get; set; }

    public int EndYearId { get; set; }

    public bool NoLongerInUse { get; set; }

    // dbo.fnCountForeignInstancesOfCriterionItemType - count of Schemes referencing this item
    // type; -1 means it is also used on a Distribution. Non-zero blocks Edit/Remove - see
    // CriterionItemTypeService.
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;
}
