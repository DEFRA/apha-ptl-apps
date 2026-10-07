namespace PTL.Auth.Entra.Claims;

/// <summary>Claim type names read from a validated Entra ID (Azure AD) id_token.</summary>
public static class EntraClaimTypes
{
    /// <summary>
    /// The directory object ID - a GUID that is stable per user per tenant, unlike the standard
    /// OIDC <c>sub</c> claim (which is pairwise/per-application by default on the v2.0 endpoint).
    /// This is the identifier persisted as fldSsoIdInt.
    /// </summary>
    public const string ObjectId = "oid";

    /// <summary>
    /// The long-form claim URI <see cref="ObjectId"/> is sometimes remapped to by inbound JWT claim
    /// type mapping, checked as a fallback.
    /// </summary>
    public const string ObjectIdLongClaimUri = "http://schemas.microsoft.com/identity/claims/objectidentifier"; // NOSONAR - stable claim-type schema identifier, not a callable/configurable endpoint

    /// <summary>
    /// On-premises Active Directory sAMAccountName for a hybrid/synced user. Not present on the
    /// id_token by default - requires the Entra ID app registration to configure it as an optional
    /// ID token claim (Azure Portal: App registration -&gt; Token configuration -&gt; Add optional
    /// claim -&gt; ID token -&gt; onprem_samaccountname).
    /// </summary>
    public const string OnPremisesSamAccountName = "onprem_samaccountname";

    /// <summary>
    /// On-premises Active Directory domain name for a hybrid/synced user. Same optional-claim
    /// prerequisite as <see cref="OnPremisesSamAccountName"/> - configure both together. Can be
    /// either NetBIOS ("DEFRA") or FQDN ("DT2.local") form depending on the on-premises AD Connect
    /// sync configuration - callers needing the short NetBIOS-style prefix should truncate at the
    /// first '.' themselves (see <c>InternalUserResolver</c>).
    /// </summary>
    public const string OnPremisesDomainName = "onprem_domainname";
}
