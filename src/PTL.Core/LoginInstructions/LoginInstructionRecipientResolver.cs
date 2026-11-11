using Microsoft.Extensions.Options;
using PTL.Core.Configuration;
using PTL.Core.LabUsers;
using PTL.Core.Participant;
using PTL.Core.TestConsultant;
using PTL.Core.Viewer;

namespace PTL.Core.LoginInstructions;

// The address, template and placeholder values a login instructions email is built from.
public sealed record LoginInstructionRecipient(string TemplateName, string TemplateId, string EmailAddress, Dictionary<string, string> Personalisation);

public interface ILoginInstructionRecipientResolver
{
    // Null when no record of the user type has the id.
    Task<LoginInstructionRecipient?> FindAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken = default);
}

public sealed class LoginInstructionRecipientResolver(
    IParticipantRepository participantRepository,
    IViewerRepository viewerRepository,
    ITestConsultantRepository testConsultantRepository,
    ILabUserRepository labUserRepository,
    IOptions<ExternalOptions> externalOptions) : ILoginInstructionRecipientResolver
{
    // The Notify personalisation field every login instructions template uses for the link to the external app.
    private const string ExternalAppLinkField = "ExternalAppLink";

    public Task<LoginInstructionRecipient?> FindAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken = default) => userType switch
    {
        LoginInstructionUserType.Participant => FindParticipantAsync(id, cancellationToken),
        LoginInstructionUserType.Viewer => FindViewerAsync(id, cancellationToken),
        LoginInstructionUserType.ExternalTestConsultant => FindTestConsultantAsync(id, cancellationToken),
        LoginInstructionUserType.LabUser => FindLabUserAsync(id, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(userType), userType, null),
    };

    // The id is the lab link's id: the email names the lab the person is being given access to.
    private async Task<LoginInstructionRecipient?> FindLabUserAsync(Guid labUserParticipantId, CancellationToken cancellationToken)
    {
        var link = await labUserRepository.FindByLinkIdAsync(labUserParticipantId, cancellationToken);
        return link is null
            ? null
            : new LoginInstructionRecipient(
                LoginInstructionEmailTemplates.LabUserName,
                LoginInstructionEmailTemplates.LabUserId,
                link.Email,
                new Dictionary<string, string>
                {
                    ["LabUserName"] = link.Name,
                    ["LabCode"] = link.LabCode,
                    ["LabName"] = link.LabName,
                    [ExternalAppLinkField] = externalOptions.Value.AppUrl,
                });
    }

    private async Task<LoginInstructionRecipient?> FindParticipantAsync(Guid id, CancellationToken cancellationToken)
    {
        var participant = await participantRepository.GetByIdAsync(id, cancellationToken);
        return participant is null
            ? null
            : new LoginInstructionRecipient(
                LoginInstructionEmailTemplates.ParticipantName,
                LoginInstructionEmailTemplates.ParticipantId,
                participant.Email,
                new Dictionary<string, string>
                {
                    ["ParticipantName"] = participant.ContactName,
                    ["LabCode"] = participant.LabCode,
                    ["LabName"] = participant.LabName,
                    [ExternalAppLinkField] = externalOptions.Value.AppUrl,
                });
    }

    private async Task<LoginInstructionRecipient?> FindViewerAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewer = (await viewerRepository.GetAllAsync(cancellationToken)).FirstOrDefault(v => v.ViewerId == id);
        return viewer is null
            ? null
            : new LoginInstructionRecipient(
                LoginInstructionEmailTemplates.ViewerName,
                LoginInstructionEmailTemplates.ViewerId,
                viewer.Email,
                new Dictionary<string, string>
                {
                    ["ViewerName"] = viewer.Name,
                    [ExternalAppLinkField] = externalOptions.Value.AppUrl,
                });
    }

    private async Task<LoginInstructionRecipient?> FindTestConsultantAsync(Guid id, CancellationToken cancellationToken)
    {
        var consultant = (await testConsultantRepository.GetAllAsync(cancellationToken)).FirstOrDefault(c => c.ExternalTestConsultantId == id);
        return consultant is null
            ? null
            : new LoginInstructionRecipient(
                LoginInstructionEmailTemplates.ExternalTestConsultantName,
                LoginInstructionEmailTemplates.ExternalTestConsultantId,
                consultant.Email,
                new Dictionary<string, string>
                {
                    ["ConsultantName"] = consultant.Name,
                    [ExternalAppLinkField] = externalOptions.Value.AppUrl,
                });
    }
}
