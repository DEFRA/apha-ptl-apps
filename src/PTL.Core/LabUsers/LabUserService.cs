using PTL.Contracts.LabUsers;
using PTL.Core.Audit;
using PTL.Core.LoginInstructions;
using PTL.Core.Participant;

namespace PTL.Core.LabUsers;

public sealed class LabUserService(
    ILabUserRepository labUserRepository,
    IParticipantRepository participantRepository,
    ILoginInstructionsService loginInstructionsService) : ILabUserService
{
    private const string NotAvailableMessage = "This lab is not available.";
    private const string AlreadyLinkedMessage = "This person is already a lab user for this lab. Use Edit to change their access.";
    private const string OtherUserTypeMessage = "This email address already belongs to a different type of PT-LIMS user, so it cannot be used for a lab user.";
    private const string EmailInUseMessage = "Another lab user already uses this email address.";
    private const string SharedMessage = "This person also works with other labs, so their name and email address cannot be changed here. You can still make them active or inactive for this lab.";

    public async Task<LabUserListResponse?> GetListAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var links = await labUserRepository.GetByParticipantAsync(participantId, cancellationToken);
        return new LabUserListResponse(participant.LabCode, participant.LabName, links.Select(ToResponse).ToList());
    }

    public async Task<LabUserDetailsResponse?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default)
    {
        var link = await labUserRepository.GetAsync(participantId, labUserId, cancellationToken);
        return link is null ? null : new LabUserDetailsResponse(link.LabCode, link.LabName, ToResponse(link));
    }

    public async Task<LabUserSaveResult?> AddAsync(Guid participantId, LabUserSaveRequest request, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        if (!participant.IsActive)
        {
            return LabUserSaveResult.Failure(string.Empty, NotAvailableMessage);
        }

        var errors = LabUserValidator.Validate(request.Name, request.Email);
        if (errors.Count > 0)
        {
            return new LabUserSaveResult(LabUserSaveOutcome.Failed, errors.ToDictionary(e => e.Key, e => e.Value));
        }

        var name = request.Name.Trim();
        var email = request.Email.Trim();
        var result = await labUserRepository.AddAsync(
            participantId,
            name,
            email,
            request.IsActive,
            labUserId => new AuditEntry(participantId, "AddLabUser", "LabUser", labUserId.ToString(), $"Lab: {participant.LabCode}; Name: {name}; Active: {request.IsActive}"),
            cancellationToken);

        return result.Outcome switch
        {
            LabUserAddOutcome.Created => LabUserSaveResult.Succeeded(LabUserSaveOutcome.Added),
            LabUserAddOutcome.AddedExistingAccount => LabUserSaveResult.Succeeded(LabUserSaveOutcome.AddedExistingAccount),
            LabUserAddOutcome.AlreadyLinked => LabUserSaveResult.Failure(LabUserValidator.EmailField, AlreadyLinkedMessage),
            _ => LabUserSaveResult.Failure(LabUserValidator.EmailField, OtherUserTypeMessage)
        };
    }

    public async Task<LabUserSaveResult?> UpdateAsync(Guid participantId, Guid labUserId, LabUserSaveRequest request, CancellationToken cancellationToken = default)
    {
        var current = await labUserRepository.GetAsync(participantId, labUserId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var errors = LabUserValidator.Validate(request.Name, request.Email);
        if (errors.Count > 0)
        {
            return new LabUserSaveResult(LabUserSaveOutcome.Failed, errors.ToDictionary(e => e.Key, e => e.Value));
        }

        var name = request.Name.Trim();
        var email = request.Email.Trim();
        var changeDetails = !string.Equals(current.Name, name, StringComparison.Ordinal)
            || !string.Equals(current.Email, email, StringComparison.OrdinalIgnoreCase);

        if (changeDetails && current.OtherLabCount > 0)
        {
            return LabUserSaveResult.Failure(string.Empty, SharedMessage);
        }

        var outcome = await labUserRepository.UpdateAsync(
            participantId,
            labUserId,
            name,
            email,
            request.IsActive,
            changeDetails,
            new AuditEntry(participantId, "UpdateLabUser", "LabUser", labUserId.ToString(), $"Lab: {current.LabCode}; Name: {name}; Active: {request.IsActive}; DetailsChanged: {changeDetails}"),
            cancellationToken);

        return outcome switch
        {
            LabUserUpdateOutcome.Updated => LabUserSaveResult.Succeeded(LabUserSaveOutcome.Updated),
            LabUserUpdateOutcome.UpdatedEmailChanged => LabUserSaveResult.Succeeded(LabUserSaveOutcome.UpdatedEmailChanged),
            LabUserUpdateOutcome.EmailUsedByOtherLabUser => LabUserSaveResult.Failure(LabUserValidator.EmailField, EmailInUseMessage),
            LabUserUpdateOutcome.EmailUsedByOtherUserType => LabUserSaveResult.Failure(LabUserValidator.EmailField, OtherUserTypeMessage),
            LabUserUpdateOutcome.SharedWithOtherLabs => LabUserSaveResult.Failure(string.Empty, SharedMessage),
            _ => null
        };
    }

    public async Task<LoginInstructionsOutcome?> SendLoginInstructionsAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default)
    {
        var link = await labUserRepository.GetAsync(participantId, labUserId, cancellationToken);
        if (link is null)
        {
            return null;
        }

        if (!link.IsActive)
        {
            return LoginInstructionsOutcome.NotActive;
        }

        var result = await loginInstructionsService.SendAsync(LoginInstructionUserType.LabUser, link.LabUserParticipantId, cancellationToken);
        return result.Outcome;
    }

    private static LabUserResponse ToResponse(LabUserLink link) => new(
        link.LabUserId,
        link.Name,
        link.Email,
        link.IsActive,
        link.LoginInstructionsSent,
        CanChangeDetails: link.OtherLabCount == 0,
        link.HasSignedIn);
}
