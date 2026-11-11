using System.Text.Json.Serialization;

namespace PTL.Contracts.ExternalUser;

/// <summary>Outcome of matching a CIDM identity to a PTL external user.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalUserResolutionStatus
{
    /// <summary>A record was matched and has access.</summary>
    Resolved,

    /// <summary>No PTL record matches the contact id or email.</summary>
    NotFound,

    /// <summary>A record matched but is inactive.</summary>
    Inactive,

    /// <summary>A Lab User matched but has no active lab relationship.</summary>
    NoActiveLab,

    /// <summary>The email matches more than one Viewer, Test Consultant or Lab User, so no single person can be chosen.</summary>
    Ambiguous,

    /// <summary>The email matches a record already linked to a different CIDM identity.</summary>
    LinkedToAnotherIdentity
}
