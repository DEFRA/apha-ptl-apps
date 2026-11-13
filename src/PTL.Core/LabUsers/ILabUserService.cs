using PTL.Contracts.LabUsers;
using PTL.Core.LoginInstructions;

namespace PTL.Core.LabUsers;

/// <summary>
/// What a lab's primary contact can do with the people who sign in to that lab. Every call is scoped to the lab:
/// a person who is not linked to it is simply not found.
/// </summary>
public interface ILabUserService
{
    /// <summary>The lab and everyone linked to it, or <see langword="null"/> when the lab does not exist.</summary>
    Task<LabUserListResponse?> GetListAsync(Guid participantId, CancellationToken cancellationToken = default);

    /// <summary>One lab user of the lab, or <see langword="null"/> when they are not linked to it.</summary>
    Task<LabUserDetailsResponse?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a lab user to the lab. When the email already belongs to a lab user, only their access to this lab is added.
    /// <see langword="null"/> when the lab does not exist.
    /// </summary>
    Task<LabUserSaveResult?> AddAsync(Guid participantId, LabUserSaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a lab user's access to the lab and, when they work with no other lab, their name and email. Making them
    /// inactive only ends their access to this lab. <see langword="null"/> when they are not linked to the lab.
    /// </summary>
    Task<LabUserSaveResult?> UpdateAsync(Guid participantId, Guid labUserId, LabUserSaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends the lab user their login instructions for this lab; <see langword="null"/> when they are not linked to it.</summary>
    Task<LoginInstructionsOutcome?> SendLoginInstructionsAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default);
}
