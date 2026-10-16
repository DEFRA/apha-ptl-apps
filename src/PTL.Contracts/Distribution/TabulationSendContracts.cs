namespace PTL.Contracts.Distribution;

// A Tabulations "Send" action (legacy GridViewMDS_RowCommand): Command is SendToTestConsultant,
// SendForAssessment or SendToResultsSignOff. ConfirmationQuestion is the legacy confirm() text;
// empty when legacy acted without asking.
public sealed record TabulationSendResponse(
    Guid MonthlyDistributionSchemeId,
    string SchemeNameFull,
    string DistributionReferenceFull,
    string Command,
    string ActionText,
    string ConfirmationQuestion);

// A Send chosen on the page but not saved yet (legacy changed the in-memory object until Save).
public sealed record TabulationStagedSendRequest(Guid MonthlyDistributionSchemeId, string Command);

public sealed record TabulationsStagedRequest(IReadOnlyList<TabulationStagedSendRequest> Staged);

// Is Command available for the scheme once the already-staged sends are applied?
public sealed record TabulationStageRequest(
    IReadOnlyList<TabulationStagedSendRequest> Staged, Guid MonthlyDistributionSchemeId, string Command);
