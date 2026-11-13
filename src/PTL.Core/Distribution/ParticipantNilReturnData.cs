namespace PTL.Core.Distribution;

public sealed record ParticipantNilReturnData(Guid DistributionParticipantId, string NilReturnReason);

public enum ParticipantNilReturnSaveStatus
{
    Saved,
    NotFound,
    DeadlinePassed,
    ReasonRequired,
    TooLong,
    HasResults,
}

public sealed record ParticipantNilReturnSaveOutcome(ParticipantNilReturnSaveStatus Status)
{
    public bool Success => Status == ParticipantNilReturnSaveStatus.Saved;
}
