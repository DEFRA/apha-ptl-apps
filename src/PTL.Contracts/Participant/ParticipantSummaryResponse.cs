namespace PTL.Contracts.Participant;

public sealed record ParticipantSummaryResponse(
    Guid ParticipantId,
    Guid CustomerId,
    string LabCode,
    string LabName,
    string ContactName,
    bool IsActive);
