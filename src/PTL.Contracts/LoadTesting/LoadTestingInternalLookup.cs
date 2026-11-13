using System.Text.Json.Serialization;

namespace PTL.Contracts.LoadTesting;

/// <summary>Body of <c>POST /api/load-testing/internal-users/lookup</c>.</summary>
public sealed record LoadTestingInternalLookupRequest(string Username);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoadTestingInternalLookupStatus
{
    /// <summary>The user exists and already has a stored directory object id.</summary>
    Found,

    /// <summary>No internal user has the username.</summary>
    NotFound,

    /// <summary>The user exists but has no stored directory object id; the lookup never links one.</summary>
    NotLinked
}

/// <summary>Result of <see cref="LoadTestingInternalLookupRequest"/>; <paramref name="SsoIdInt"/> is set only when <paramref name="Status"/> is Found.</summary>
public sealed record LoadTestingInternalLookupResponse(LoadTestingInternalLookupStatus Status, Guid? SsoIdInt);
