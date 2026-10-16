namespace PTL.Contracts.Distribution;

// Distribution Participants screen (legacy Scheduling/DistributionParticipantList.aspx).
public sealed record DistributionParticipantsResponse(
    Guid MonthlyDistributionSchemeId,
    int YearId,
    int MonthId,
    string SchemeIdentifier,
    string SchemeName,
    string DistributionReferenceFull,
    bool IsRepeatDistribution,
    IReadOnlyList<DistributionParticipantResponse> Participants,
    IReadOnlyList<DistributionAdditionalParticipantResponse> AdditionalParticipants);

public sealed record DistributionParticipantResponse(
    Guid DistributionParticipantId,
    string FullName,
    DateTime DeadlineDate,
    string DeadlineChangeReason,
    bool AdditionalParticipant);

public sealed record DistributionAdditionalParticipantResponse(
    Guid ParticipantSchemeId,
    string FullName);

public sealed record ChangeDeadlineRequest(DateTime? NewDeadlineDate, string? Reason);

public sealed record ChangeDeadlineResult(bool Success, string? NewDeadlineDateError, string? ReasonError);

public sealed record RepeatDistributionRequest(IReadOnlyList<ParticipantSelectionRequest> Selections);

public sealed record ParticipantSelectionRequest(Guid Id, bool IsAdditional);

public sealed record RepeatDistributionResult(bool Success, string? Error);
