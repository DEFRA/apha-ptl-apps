using PTL.Core.Audit;

namespace PTL.Core.LabUsers;

/// <summary>What adding a lab user did. The numbers are the <c>Outcome</c> values <c>spiLabUserForParticipant</c> returns.</summary>
public enum LabUserAddOutcome
{
    /// <summary>A new person and their link to the lab were created.</summary>
    Created = 1,

    /// <summary>The email already belonged to a lab user, so only the link to this lab was added.</summary>
    AddedExistingAccount = 2,

    /// <summary>The person is already linked to this lab; nothing changed.</summary>
    AlreadyLinked = 3,

    /// <summary>The email belongs to a Participant, Viewer or Test Consultant.</summary>
    EmailUsedByOtherUserType = 4
}

/// <summary>What editing a lab user did. The numbers are the <c>Outcome</c> values <c>spuLabUserForParticipant</c> returns.</summary>
public enum LabUserUpdateOutcome
{
    /// <summary>The person is not a lab user of this lab.</summary>
    NotFound = 0,

    Updated = 1,

    /// <summary>The new email already belongs to another lab user.</summary>
    EmailUsedByOtherLabUser = 2,

    /// <summary>The person also works with other labs, so their name and email cannot be changed from here.</summary>
    SharedWithOtherLabs = 3,

    /// <summary>The new email belongs to a Participant, Viewer or Test Consultant.</summary>
    EmailUsedByOtherUserType = 4,

    /// <summary>Updated, and the email changed: the linked CIDM account was cleared and the login instructions must be sent again.</summary>
    UpdatedEmailChanged = 5
}

/// <param name="Outcome">What happened.</param>
/// <param name="LabUserId">The person's id when one was added or found.</param>
public sealed record LabUserAddResult(LabUserAddOutcome Outcome, Guid? LabUserId);

public interface ILabUserRepository
{
    /// <summary>Everyone linked to the lab, active or not, by name.</summary>
    Task<IReadOnlyList<LabUserLink>> GetByParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);

    /// <summary>One person's link to the lab, or <see langword="null"/> when they are not a lab user of it.</summary>
    Task<LabUserLink?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default);

    /// <summary>A link by its own id, for the login instructions email.</summary>
    Task<LabUserLink?> FindByLinkIdAsync(Guid labUserParticipantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the person to the lab: a new person when the email is new, otherwise just the link. The audit entry is written in
    /// the same transaction, only when something was added; <paramref name="auditFor"/> is given the person's id.
    /// </summary>
    Task<LabUserAddResult> AddAsync(
        Guid participantId, string name, string email, bool isActive, Func<Guid, AuditEntry> auditFor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the person's access to the lab and, when <paramref name="changeDetails"/> is true, their name and email. The audit
    /// entry is written in the same transaction, only when something was changed.
    /// </summary>
    Task<LabUserUpdateOutcome> UpdateAsync(
        Guid participantId, Guid labUserId, string name, string email, bool isActive, bool changeDetails, AuditEntry audit, CancellationToken cancellationToken = default);
}
