namespace PTL.ExternalWeb.Features.Account;

/// <summary>The <c>reason</c> values the sign-in denial page understands, so the resolver and the view cannot drift apart.</summary>
public static class NotPermittedReasons
{
    /// <summary>The matched record exists but is inactive.</summary>
    public const string Inactive = "inactive";

    /// <summary>A Lab User matched, but none of their lab relationships is active.</summary>
    public const string NoActiveLab = "no-active-lab";

    /// <summary>The path of the denial page, with the optional reason.</summary>
    public static string PathFor(string? reason = null) =>
        reason is null ? "/Account/NotPermitted" : $"/Account/NotPermitted?reason={reason}";
}
