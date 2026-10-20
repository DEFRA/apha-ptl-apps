namespace PTL.Core.Distribution;

// What an enabled Tabulations action does when chosen (legacy GridViewMDS_RowCommand CommandName).
public enum TabulationCommand
{
    None,
    SendToTestConsultant,
    SendForAssessment,
    SendToResultsSignOff,
    ViewComments,
}

// One of the legacy Actions-column controls after RowDataBound has applied its state. Hint is the
// legacy ToolTip text; IsOverdue is the legacy red colouring; IsLabel marks the sign-off link that
// legacy swaps for a plain red "OVERDUE" label.
public sealed record TabulationAction(
    string Text, bool IsEnabled, string Hint, TabulationCommand Command, bool IsOverdue = false, bool IsLabel = false);

public sealed record TabulationSchemeActions(
    TabulationAction AmendResults,
    TabulationAction SendToTestConsultant,
    TabulationAction SendForAssessment,
    TabulationAction SendToResultsSignOff);

public sealed record TabulationRow(Guid TabulationId, string Name, bool IsTestConsultant, int PublishCount);

// Consultants holds the Test Consultants (1-3) or, for assessment schemes, the Assessors (1-4);
// blank slots are kept because legacy always renders every numbered row.
public sealed record TabulationsScheme(
    Guid MonthlyDistributionSchemeId,
    string SchemeNameFull,
    string DistributionReferenceFull,
    DateTime DeadlineDate,
    DateTime ResultsIssueTargetDate,
    bool RequiresAssessment,
    IReadOnlyList<string> Consultants,
    IReadOnlyList<TabulationRow> Tabulations,
    TabulationSchemeActions Actions);

public sealed record TabulationsPage(int YearId, int MonthId, IReadOnlyList<TabulationsScheme> Schemes);
