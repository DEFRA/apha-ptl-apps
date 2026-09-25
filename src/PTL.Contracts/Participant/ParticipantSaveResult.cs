using System.Diagnostics.CodeAnalysis;

namespace PTL.Contracts.Participant;

// Returned by PTL.ApiClient's CreateParticipantAsync/UpdateParticipantAsync so callers can surface
// validation errors (HTTP 400) without needing to catch an HttpRequestException. FieldErrors is
// keyed by the ParticipantFormViewModel property name (empty key "" for a non-field-specific error),
// matching ASP.NET Core ModelState conventions, so InternalWeb can attach each message to its
// exact field and render a GOV.UK error summary that links to it.
public sealed record ParticipantSaveResult(
    [property: MemberNotNullWhen(true, nameof(Participant))] bool Success,
    ParticipantResponse? Participant,
    IReadOnlyDictionary<string, string[]> FieldErrors);
