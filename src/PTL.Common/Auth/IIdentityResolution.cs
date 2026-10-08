namespace PTL.Common.Auth;

/// <summary>
/// Common shape of a validated-identity resolution outcome, shared by every PTL OIDC provider
/// (CIDM for external users, Entra ID for internal users) so their near-identical
/// "resolve against our own user record, or deny sign-in" logic can live in one place.
/// </summary>
public interface IIdentityResolution
{
    /// <summary>Gets a value indicating whether sign-in is permitted.</summary>
    bool IsAllowed { get; }

    /// <summary>Gets the local path to redirect to instead, when <see cref="IsAllowed"/> is <see langword="false"/>.</summary>
    string? DenialRedirectPath { get; }

    /// <summary>Gets additional claims to add to the principal, when <see cref="IsAllowed"/> is <see langword="true"/>.</summary>
    IReadOnlyDictionary<string, string>? Claims { get; }
}
