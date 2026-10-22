namespace PTL.Core.CategoryItemType;

// tblCategoryItemType - year-versioned reference list consumed as an "Add Category" dropdown
// option on the Scheme Tests tab (PTL.Core.Lookup.SchemeItemTypeKind.CategoryItemType is the
// read-only projection used there; this richer shape backs the Category Items admin screen).
public sealed class CategoryItemType
{
    public Guid CategoryItemTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int StartYearId { get; set; }

    public int EndYearId { get; set; }

    public bool NoLongerInUse { get; set; }

    // dbo.fnCountForeignInstancesOfCategoryItemType - count of Schemes referencing this item type;
    // -1 means it is also used on a Distribution. Non-zero blocks Edit/Remove - see
    // CategoryItemTypeService.
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;
}
