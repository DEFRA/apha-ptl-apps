using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Audit;
using PTL.Core.Configuration;
using PTL.Core.Notifications;
using PTL.Core.Participant;
using PTL.Core.TestConsultant;
using PTL.Core.Viewer;

namespace PTL.Core.LoginInstructions;

public sealed partial class LoginInstructionsService(
    IParticipantRepository participantRepository,
    IViewerRepository viewerRepository,
    ITestConsultantRepository testConsultantRepository,
    ILoginInstructionsRepository loginInstructionsRepository,
    INotifyClient notifyClient,
    IOptions<ExternalOptions> externalOptions,
    IActingUserAccessor actingUser,
    TimeProvider timeProvider,
    ILogger<LoginInstructionsService> logger) : ILoginInstructionsService
{
    private sealed record Recipient(string TemplateName, string TemplateId, string EmailAddress, Dictionary<string, string> Personalisation);

    public async Task<LoginInstructionsResult> SendAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken = default)
    {
        var recipient = await FindRecipientAsync(userType, id, cancellationToken);
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

    private Task<Recipient?> FindRecipientAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken) => userType switch
    {
        LoginInstructionUserType.Participant => FindParticipantAsync(id, cancellationToken),
        LoginInstructionUserType.Viewer => FindViewerAsync(id, cancellationToken),
        LoginInstructionUserType.ExternalTestConsultant => FindTestConsultantAsync(id, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(userType), userType, null),
    };

    private async Task<Recipient?> FindParticipantAsync(Guid id, CancellationToken cancellationToken)
    {
        var participant = await participantRepository.GetByIdAsync(id, cancellationToken);
        return participant is null
            ? null
            : new Recipient(
                LoginInstructionEmailTemplates.ParticipantName,
                LoginInstructionEmailTemplates.ParticipantId,
                participant.Email,
                new Dictionary<string, string>
                {
                    ["ParticipantName"] = participant.ContactName,
                    ["LabCode"] = participant.LabCode,
                    ["LabName"] = participant.LabName,
                    ["ExternalAppLink"] = externalOptions.Value.AppUrl,
                });
    }

    private async Task<Recipient?> FindViewerAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewer = (await viewerRepository.GetAllAsync(cancellationToken)).FirstOrDefault(v => v.ViewerId == id);
        return viewer is null
            ? null
            : new Recipient(
                LoginInstructionEmailTemplates.ViewerName,
                LoginInstructionEmailTemplates.ViewerId,
                viewer.Email,
                new Dictionary<string, string>
                {
                    ["ViewerName"] = viewer.Name,
                    ["ExternalAppLink"] = externalOptions.Value.AppUrl,
                });
    }

    private async Task<Recipient?> FindTestConsultantAsync(Guid id, CancellationToken cancellationToken)
    {
        var consultant = (await testConsultantRepository.GetAllAsync(cancellationToken)).FirstOrDefault(c => c.ExternalTestConsultantId == id);
        return consultant is null
            ? null
            : new Recipient(
                LoginInstructionEmailTemplates.ExternalTestConsultantName,
                LoginInstructionEmailTemplates.ExternalTestConsultantId,
                consultant.Email,
                new Dictionary<string, string>
                {
                    ["ConsultantName"] = consultant.Name,
                    ["ExternalAppLink"] = externalOptions.Value.AppUrl,
                });
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
