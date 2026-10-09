namespace PTL.Core.Scheme;

// The Tests tab tree. Legacy keeps the whole tree in ViewState and only writes it on Save, so
// these are staged in the form and persisted as one unit by SchemeRepository.
public sealed class SchemeTest
{
    public Guid TestId { get; set; }
    public Guid TestTypeId { get; set; }
    public string TestType { get; set; } = string.Empty;
    public Guid SchemeId { get; set; }
    public int Order { get; set; }

    public IList<SchemeTestResultItem> ResultItems { get; set; } = [];
    public IList<SchemeTestMethodItem> MethodItems { get; set; } = [];

    // Only populated for schemes that require assessment.
    public IList<SchemeCategoryItem> Categories { get; set; } = [];
}

public sealed class SchemeTestResultItem
{
    public Guid TestResultItemId { get; set; }
    public Guid TestResultItemTypeId { get; set; }
    public string TestResultItemType { get; set; } = string.Empty;
    public Guid TestId { get; set; }
    public int Order { get; set; }

    // tblTestReturnValueType.fldExpectedLength, joined via the item's type - display-only, drives
    // the Printable Scheme worksheet's column-width packing (legacy ResultsTable.GetColumnWidth).
    public int ExpectedLength { get; set; }
}

public sealed class SchemeTestMethodItem
{
    public Guid TestMethodItemId { get; set; }
    public Guid TestMethodItemTypeId { get; set; }
    public string TestMethodItemType { get; set; } = string.Empty;
    public Guid TestId { get; set; }
    public int Order { get; set; }

    // See SchemeTestResultItem.ExpectedLength - same joined column, same display-only purpose.
    public int ExpectedLength { get; set; }
}

public sealed class SchemeCategoryItem
{
    public Guid CategoryItemId { get; set; }
    public Guid CategoryItemTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TestId { get; set; }
    public int Order { get; set; }

    public IList<SchemeCriterionItem> Criteria { get; set; } = [];
}

public sealed class SchemeCriterionItem
{
    public Guid CriterionItemId { get; set; }
    public Guid CriterionItemTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CategoryItemId { get; set; }
    public int Order { get; set; }
}
