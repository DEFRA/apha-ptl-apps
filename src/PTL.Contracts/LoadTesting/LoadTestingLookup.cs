using System.Text.Json.Serialization;

namespace PTL.Contracts.LoadTesting;

public static class LoadTestingHeaders
{
    /// <summary>Sent as "true" by PTL.ExternalWeb only while its own <c>LoadTesting:Enabled</c> is on; PTL.Api answers 404 without it.</summary>
    public const string Enabled = "X-Load-Testing";
}

/// <summary>Body of <c>POST /api/load-testing/external-users/lookup</c>.</summary>
public sealed record LoadTestingLookupRequest(string Email);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoadTestingLookupStatus
{
    /// <summary>One CIDM contact id is already stored for the email's user.</summary>
    Found,

    /// <summary>No external user has the email.</summary>
    NotFound,

    /// <summary>The user exists but has no stored contact id; the lookup never links one.</summary>
    NotLinked,

    /// <summary>The user's records carry more than one contact id.</summary>
    Ambiguous
}

/// <summary>Result of <see cref="LoadTestingLookupRequest"/>; <paramref name="SsoIdExt"/> is set only when <paramref name="Status"/> is Found.</summary>
public sealed record LoadTestingLookupResponse(LoadTestingLookupStatus Status, Guid? SsoIdExt);
