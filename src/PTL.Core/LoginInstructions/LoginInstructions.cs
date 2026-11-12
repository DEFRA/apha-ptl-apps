using PTL.Contracts.LoginInstructions;
using PTL.Core.Audit;

namespace PTL.Core.LoginInstructions;

public enum LoginInstructionUserType
{
    Participant,
    Viewer,
    ExternalTestConsultant,

    /// <summary>A lab user; the id is the lab link's own id (<c>tlnkLabUserParticipant</c>), because the email names the lab.</summary>
    LabUser,
}

public enum LoginInstructionsOutcome
{
    Sent,
    NotFound,
    NoEmailAddress,
    SendFailed,

    /// <summary>The user (or the lab user's access to the lab) is inactive, so no instructions are sent.</summary>
    NotActive,
}

public sealed record LoginInstructionsResult(LoginInstructionsOutcome Outcome)
{
    public bool Success => Outcome == LoginInstructionsOutcome.Sent;
}

// The response every caller sends for an outcome other than "the record was not found" (which each caller reports in its own way).
public static class LoginInstructionsOutcomeExtensions
{
    public static LoginInstructionsResponse ToResponse(this LoginInstructionsOutcome outcome) => outcome switch
    {
        LoginInstructionsOutcome.Sent => new LoginInstructionsResponse(true),
        LoginInstructionsOutcome.NoEmailAddress => new LoginInstructionsResponse(false, "There is no email address saved to send the login instructions to."),
        LoginInstructionsOutcome.NotActive => new LoginInstructionsResponse(false, "Login instructions can only be sent to an active user."),
        _ => new LoginInstructionsResponse(false, "The login instructions email could not be sent. Please try again."),
    };
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

    public const string LabUserName = "lab-user-login-instructions";
    public const string LabUserId = "72662826-7561-4506-ab41-b36a9035f1ea";
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
