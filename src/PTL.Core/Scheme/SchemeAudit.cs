namespace PTL.Core.Scheme;

public enum SchemeAuditOperation
{
    Insert,
    Update,
    Delete,
}

// One row for spiAuditTest, spiAuditTestMethodItem, spiAuditTestResultItem, spiAuditCategoryItem or
// spiAuditCriterionItem. TypeId and Order are the new values, or the stored ones for a delete.
public sealed record SchemeItemAudit(
    Guid Id,
    SchemeAuditOperation Operation,
    Guid TypeId,
    bool TypeChanged,
    int Order,
    bool OrderChanged);

// One row for spiAuditTabulation.
public sealed record SchemeTabulationAudit(
    Guid Id,
    SchemeAuditOperation Operation,
    string Name,
    bool NameChanged,
    bool IntendedResultsOnly,
    bool IntendedResultsOnlyChanged);

// One row for spiAuditTabulationTestMethodItem or spiAuditTabulationTestResultItem. Links are only
// ever inserted or deleted; neither table has an update procedure. An inserted link's id is chosen
// when the plan is built, so the audit row and the saved row share it.
public sealed record SchemeTabulationLinkAudit(Guid LinkId, SchemeAuditOperation Operation, Guid TabulationId, Guid ItemId);

// Everything legacy's AuditableBusinessBase.AuditableCollection would have found on the Scheme.
public sealed record SchemeAuditPlan(
    IReadOnlyList<SchemeItemAudit> Tests,
    IReadOnlyList<SchemeItemAudit> TestMethodItems,
    IReadOnlyList<SchemeItemAudit> TestResultItems,
    IReadOnlyList<SchemeItemAudit> CategoryItems,
    IReadOnlyList<SchemeItemAudit> CriterionItems,
    IReadOnlyList<SchemeTabulationAudit> Tabulations,
    IReadOnlyList<SchemeTabulationLinkAudit> MethodLinks,
    IReadOnlyList<SchemeTabulationLinkAudit> ResultLinks)
{
    public bool IsEmpty =>
        Tests.Count == 0 && TestMethodItems.Count == 0 && TestResultItems.Count == 0 && CategoryItems.Count == 0
        && CriterionItems.Count == 0 && Tabulations.Count == 0 && MethodLinks.Count == 0 && ResultLinks.Count == 0;
}

// The reason captured by legacy Audit.aspx plus the changes it covers, handed to the repository so
// the audit rows and the Scheme update are written in one transaction.
public sealed record SchemeAuditWrite(string AuditWho, string AuditWhy, SchemeAuditPlan Plan);

public enum SchemeUpdateStatus
{
    Updated,
    NotFound,
    AuditReasonRequired,
}

public sealed record SchemeUpdateResult(SchemeUpdateStatus Status, Scheme? Scheme = null, string AuditObjectName = "");
