using PTL.Core.Customer;
using PTL.Core.Participant;
using PTL.Core.TestConsultant;
using PTL.Core.Viewer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Core.ExternalUser;

public sealed class ExternalUserService(
    IParticipantRepository participantRepository,
    IViewerRepository viewerRepository,
    ITestConsultantRepository testConsultantRepository,
    ICustomerRepository customerRepository) : IExternalUserService
{
    private const string ParticipantRoleName = "Participant";
    private const string ViewerRoleName = "Viewer";
    private const string TestConsultantRoleName = "Test Consultant";

    public async Task<ExternalUserResolutionResult> ResolveAsync(
        Guid ssoIdExt,
        string email,
        string displayName,
        IReadOnlyList<string> cidmRoles,
        CancellationToken cancellationToken = default)
    {
        var resolvedRoles = new List<string>();
        Guid? participantId = null;
        string? labCode = null;
        Guid? viewerId = null;
        Guid? testConsultantId = null;
        var canOrderOnline = false;

        if (HasRole(cidmRoles, ParticipantRoleName))
        {
            var participant = await ResolveParticipantAsync(ssoIdExt, email, cancellationToken);
            if (participant is not null)
            {
                participantId = participant.ParticipantId;
                labCode = participant.LabCode;
                resolvedRoles.Add(ParticipantRoleName);

                var customer = await customerRepository.GetByIdAsync(participant.CustomerId, cancellationToken);
                canOrderOnline = customer?.CanOrderOnline ?? false;
            }
        }

        if (HasRole(cidmRoles, ViewerRoleName))
        {
            viewerId = await ResolveViewerAsync(ssoIdExt, email, cancellationToken);
            if (viewerId is not null)
            {
                resolvedRoles.Add(ViewerRoleName);
            }
        }

        if (HasRole(cidmRoles, TestConsultantRoleName))
        {
            testConsultantId = await ResolveTestConsultantAsync(ssoIdExt, email, cancellationToken);
            if (testConsultantId is not null)
            {
                resolvedRoles.Add(TestConsultantRoleName);
            }
        }

        return new ExternalUserResolutionResult(displayName, resolvedRoles, participantId, labCode, viewerId, testConsultantId, canOrderOnline);
    }

    private static bool HasRole(IReadOnlyList<string> cidmRoles, string roleName) =>
        cidmRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    private async Task<CoreParticipant?> ResolveParticipantAsync(Guid ssoIdExt, string email, CancellationToken cancellationToken)
    {
        var participant = await participantRepository.GetBySsoIdExtAsync(ssoIdExt, cancellationToken);
        if (participant is not null)
        {
            return participant;
        }

        participant = await participantRepository.GetByEmailAsync(email, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        participant.SsoIdExt = ssoIdExt;
        await participantRepository.UpdateAsync(participant, cancellationToken);
        return participant;
    }

    private async Task<Guid?> ResolveViewerAsync(Guid ssoIdExt, string email, CancellationToken cancellationToken)
    {
        var viewer = await viewerRepository.GetBySsoIdExtAsync(ssoIdExt, cancellationToken);
        if (viewer is not null)
        {
            return viewer.ViewerId;
        }

        viewer = await viewerRepository.GetByEmailAsync(email, cancellationToken);
        if (viewer is null)
        {
            return null;
        }

        viewer.SsoIdExt = ssoIdExt;
        await viewerRepository.UpdateAsync(viewer, cancellationToken);
        return viewer.ViewerId;
    }

    private async Task<Guid?> ResolveTestConsultantAsync(Guid ssoIdExt, string email, CancellationToken cancellationToken)
    {
        var testConsultant = await testConsultantRepository.GetBySsoIdExtAsync(ssoIdExt, cancellationToken);
        if (testConsultant is not null)
        {
            return testConsultant.ExternalTestConsultantId;
        }

        testConsultant = await testConsultantRepository.GetByEmailAsync(email, cancellationToken);
        if (testConsultant is null)
        {
            return null;
        }

        testConsultant.SsoIdExt = ssoIdExt;
        await testConsultantRepository.UpdateAsync(testConsultant, cancellationToken);
        return testConsultant.ExternalTestConsultantId;
    }
}
