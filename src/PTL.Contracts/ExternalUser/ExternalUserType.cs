using System.Text.Json.Serialization;

namespace PTL.Contracts.ExternalUser;

/// <summary>
/// The kind of PTL record a CIDM identity was matched to. The numeric value is the match priority (lowest
/// wins when the same person exists in more than one table) and must match <c>vwExternalUser.UserType</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalUserType
{
    Participant = 1,
    Viewer = 2,
    TestConsultant = 3,
    LabUser = 4
}
