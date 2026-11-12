namespace PTL.Contracts.Distribution;

// External participant Nil Return: explicit non-submission with a mandatory reason.
public sealed record ParticipantNilReturnResponse(bool HasNilReturn, string NilReturnReason);

public sealed record ParticipantNilReturnSaveRequest(string? NilReturnReason);

public sealed record ParticipantNilReturnSaveResult(
    bool Success,
    bool DeadlinePassed = false,
    bool ReasonRequired = false,
    bool HasResults = false);
