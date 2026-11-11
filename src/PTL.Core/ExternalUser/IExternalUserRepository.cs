using PTL.Contracts.ExternalUser;

namespace PTL.Core.ExternalUser;

/// <summary>Reads and links PTL external users (Participant, Viewer, Test Consultant, Lab User) to CIDM identities in one round trip each.</summary>
public interface IExternalUserRepository
{
    /// <summary>Everything linked to a CIDM contact id, highest-priority user type first.</summary>
    Task<IReadOnlyList<ExternalUserMatch>> FindBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default);

    /// <summary>Every user carrying the email, linked or not, highest-priority user type first.</summary>
    Task<IReadOnlyList<ExternalUserMatch>> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links the contact id to the not-yet-linked records of <paramref name="userType"/> carrying the email, then returns
    /// everything now linked to it. A record already linked to any identity is never overwritten.
    /// </summary>
    Task<IReadOnlyList<ExternalUserMatch>> LinkAsync(ExternalUserType userType, string email, Guid ssoIdExt, CancellationToken cancellationToken = default);
}
