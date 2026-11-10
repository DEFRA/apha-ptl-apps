using Microsoft.Extensions.Logging;
using PTL.Core.Audit;
using PTL.Core.Notifications;

namespace PTL.Core.LoginInstructions;

public sealed partial class LoginInstructionsService(
    ILoginInstructionRecipientResolver recipientResolver,
    ILoginInstructionsRepository loginInstructionsRepository,
    INotifyClient notifyClient,
    IActingUserAccessor actingUser,
    TimeProvider timeProvider,
    ILogger<LoginInstructionsService> logger) : ILoginInstructionsService
{
    public async Task<LoginInstructionsResult> SendAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken = default)
    {
        var recipient = await recipientResolver.FindAsync(userType, id, cancellationToken);
        if (recipient is null)
        {
            return new LoginInstructionsResult(LoginInstructionsOutcome.NotFound);
        }

        if (string.IsNullOrWhiteSpace(recipient.EmailAddress))
        {
            LogNoEmailAddress(logger, userType, id);
            return new LoginInstructionsResult(LoginInstructionsOutcome.NoEmailAddress);
        }

        try
        {
            await notifyClient.SendEmailAsync(recipient.TemplateId, recipient.EmailAddress, recipient.Personalisation, id.ToString(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendFailed(logger, userType, id, recipient.TemplateId, ex);
            return new LoginInstructionsResult(LoginInstructionsOutcome.SendFailed);
        }

        // Notify has the email, so recording it is not cancellable: stopping here would leave the "Send Instructions" link offered again.
        var audit = new AuditEntry(actingUser.ActingUserId, "SendLoginInstructions", userType.ToString(), id.ToString(), $"Template: {recipient.TemplateName}");
        await loginInstructionsRepository.MarkSentAsync(userType, id, timeProvider.GetUtcNow().UtcDateTime, audit, CancellationToken.None);

        LogSent(logger, userType, id, recipient.TemplateId);
        return new LoginInstructionsResult(LoginInstructionsOutcome.Sent);
    }

    [LoggerMessage(EventId = 101, Level = LogLevel.Information,
        Message = "Login instructions email sent. UserType={UserType} UserId={UserId} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, LoginInstructionUserType userType, Guid userId, string templateId);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error,
        Message = "Login instructions email could not be sent; the record stays unsent. UserType={UserType} UserId={UserId} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, LoginInstructionUserType userType, Guid userId, string templateId, Exception exception);

    [LoggerMessage(EventId = 103, Level = LogLevel.Warning,
        Message = "Login instructions email not sent: the record has no email address. UserType={UserType} UserId={UserId}")]
    private static partial void LogNoEmailAddress(ILogger logger, LoginInstructionUserType userType, Guid userId);
}
