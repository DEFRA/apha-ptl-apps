using PTL.Core.Audit;

namespace PTL.Core.LoginInstructions;

public enum LoginInstructionUserType
{
    Participant,
    Viewer,
    ExternalTestConsultant,
}

public enum LoginInstructionsOutcome
{
    Sent,
    NotFound,
    NoEmailAddress,
    SendFailed,
}

public sealed record LoginInstructionsResult(LoginInstructionsOutcome Outcome)
{
    public bool Success => Outcome == LoginInstructionsOutcome.Sent;
}

// Fixed - the same GOV.UK Notify templates are used in every environment, so they are not configuration.
public static class LoginInstructionEmailTemplates
{
    public const string ViewerName = "viewer-login-instructions";
    public const string ViewerId = "03d26290-c022-4d96-93bb-416cd64e806d";

    public const string ExternalTestConsultantName = "external-test-consultant-login-instructions";
    public const string ExternalTestConsultantId = "d1f17c97-f031-4f9b-a537-d6916f3b2d50";

    public const string ParticipantName = "participant-login-instructions";
    public const string ParticipantId = "64091cdb-eaf5-4c55-8680-84a727b87c91";
}

public interface ILoginInstructionsService
{
    // Sends the user type's login instructions email to the address saved on the record and, only once GOV.UK
    // Notify has accepted it, records it as sent. A failed send leaves the record unsent.
    Task<LoginInstructionsResult> SendAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken = default);
}

public interface ILoginInstructionsRepository
{
    // Sets LoginInstructionsSent and its date on the record, with the audit entry in the same transaction.
    Task MarkSentAsync(LoginInstructionUserType userType, Guid id, DateTime sentDate, AuditEntry auditEntry, CancellationToken cancellationToken = default);
}
