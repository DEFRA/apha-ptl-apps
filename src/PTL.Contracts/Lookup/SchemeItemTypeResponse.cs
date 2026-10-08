namespace PTL.Contracts.Lookup;

// The five "Add" dropdowns on the Scheme Tests tab. Each is backed by its own
// spgXxxTypeByYearId procedure, all of which take @YearId int and filter on
// fldStartYearId <= @YearId <= fldEndYearId.
public enum SchemeItemTypeKind
{
    TestType,
    TestResultItemType,
    TestMethodItemType,
    CategoryItemType,
    CriterionItemType,
}

public sealed record SchemeItemTypeResponse(Guid ItemTypeId, string Name, bool NoLongerInUse);
