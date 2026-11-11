using PTL.Contracts.ExternalUser;

namespace PTL.Core.ExternalUser;

/// <summary>Resolves a CIDM-authenticated external user to exactly one PTL user type.</summary>
public interface IExternalUserService
{
    /// <summary>
    /// Finds the user by CIDM contact id. A user not yet linked is matched by email across the Participant, Viewer, Test
    /// Consultant and Lab User tables - the first table with a match decides the user type - and linked to the contact id
    /// so later sign-ins never use the email again. No record is ever created.
    /// </summary>
    Task<ExternalUserResolutionResult> ResolveAsync(
        Guid ssoIdExt,
        string email,
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>The active labs a signed-in Participant or Lab User can work on, by contact id; empty for any other user.</summary>
    Task<IReadOnlyList<ExternalLab>> GetLabsAsync(Guid ssoIdExt, CancellationToken cancellationToken = default);

    /// <summary>
    /// The lab to work on, only when it is one of the user's active labs - the check that makes a chosen lab trustworthy -
    /// otherwise <see langword="null"/>.
    /// </summary>
    Task<ExternalLabContext?> GetLabAsync(Guid ssoIdExt, Guid participantId, CancellationToken cancellationToken = default);
}
