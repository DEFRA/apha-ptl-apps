namespace PTL.Contracts.Distribution;

// Monthly Distributions - Tabulations (legacy Scheduling/Tabulations.aspx, sitemap title "Tabulations").
public sealed record TabulationsResponse(int YearId, int MonthId, IReadOnlyList<TabulationsSchemeResponse> Schemes);

// Consultants = the Test Consultants (1-3) or, for assessment schemes, the Assessors (1-4); blank
// slots are kept because legacy always renders every numbered row.
public sealed record TabulationsSchemeResponse(
    Guid MonthlyDistributionSchemeId,
    string SchemeNameFull,
    string DistributionReferenceFull,
    DateTime DeadlineDate,
    DateTime ResultsIssueTargetDate,
    bool RequiresAssessment,
    IReadOnlyList<string> Consultants,
    IReadOnlyList<TabulationRowResponse> Tabulations,
    TabulationActionsResponse Actions);

// IsTestConsultant = legacy "TC" marker; PublishCount = the number beside "Issued" (0 hides the link).
public sealed record TabulationRowResponse(Guid TabulationId, string Name, bool IsTestConsultant, int PublishCount);

public sealed record TabulationActionsResponse(
    TabulationActionResponse AmendResults,
    TabulationActionResponse SendToTestConsultant,
    TabulationActionResponse SendForAssessment,
    TabulationActionResponse SendToResultsSignOff);

// Command = what choosing the action does: None (navigation), SendToTestConsultant, SendForAssessment,
// SendToResultsSignOff or ViewComments. Hint is the legacy tooltip text; IsOverdue is the legacy red
// colouring; IsLabel marks the sign-off link legacy replaces with a plain red "OVERDUE" label.
public sealed record TabulationActionResponse(
    string Text, bool IsEnabled, string Hint, string Command, bool IsOverdue = false, bool IsLabel = false);
