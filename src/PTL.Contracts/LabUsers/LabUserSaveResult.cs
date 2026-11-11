using System.Text.Json.Serialization;

namespace PTL.Contracts.LabUsers;

/// <summary>What saving a lab user did.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LabUserSaveOutcome
{
    /// <summary>Nothing was saved - see the field errors.</summary>
    Failed,

    /// <summary>A new person was added to the lab.</summary>
    Added,

    /// <summary>The person already had an account (from another lab), so only their access to this lab was added.</summary>
    AddedExistingAccount,

    /// <summary>The person's details or access were changed.</summary>
    Updated,

    /// <summary>As <see cref="Updated"/>, and the email address changed, so the person must sign in with the new address and be sent the instructions again.</summary>
    UpdatedEmailChanged
}

/// <summary>The result of adding or editing a lab user. <see cref="FieldErrors"/> is keyed by field name ("" for an error about the whole form).</summary>
public sealed record LabUserSaveResult(LabUserSaveOutcome Outcome, IReadOnlyDictionary<string, string[]> FieldErrors)
{
    public bool Success => Outcome != LabUserSaveOutcome.Failed;

    public static LabUserSaveResult Succeeded(LabUserSaveOutcome outcome) =>
        new(outcome, new Dictionary<string, string[]>());

    public static LabUserSaveResult Failure(string field, string message) =>
        new(LabUserSaveOutcome.Failed, new Dictionary<string, string[]> { [field] = [message] });
}
