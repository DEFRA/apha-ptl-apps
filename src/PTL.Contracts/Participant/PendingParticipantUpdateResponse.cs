namespace PTL.Contracts.Participant;

// One row on the "Review Pending Participant Updates" list (legacy
// ReviewPendingParticipantUpdates.aspx / spgaPendingParticipantDetailsEditInfo).
public sealed record PendingParticipantUpdateSummaryResponse(
    Guid ParticipantId,
    string LabCode,
    string LabName);

// The proposed changes submitted by an external participant, awaiting admin approval/decline
// (legacy tblPendingParticipantDetailsEdit / spgPendingParticipantDetailsEditByParticipantID).
// Field set matches PendingParticipantUpdateDetails.aspx exactly - contact fields only, no
// invoice block, and both email addresses.
public sealed record PendingParticipantUpdateResponse(
    Guid ParticipantId,
    Guid CustomerId,
    string LabCode,
    string ContactName,
    string Organisation,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    Guid CountryId,
    string Telephone,
    string Fax,
    string Email,
    string Email2);

// Current participant record + the pending change proposed against it, for the
// PendingParticipantUpdateDetails comparison page.
public sealed record PendingParticipantUpdateComparisonResponse(
    ParticipantResponse Current,
    PendingParticipantUpdateResponse Pending);

// Amended pending values submitted alongside an Approve, mirroring legacy
// PendingParticipantUpdateDetails.aspx's ButtonApprove_Click, which writes the on-screen values to
// both the pending record and the live participant rather than approving the stored values verbatim.
public sealed record PendingParticipantUpdateSaveRequest(
    string ContactName,
    string Organisation,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    Guid CountryId,
    string Telephone,
    string Fax,
    string Email,
    string Email2);

// Outcome of an Approve/Decline. NotFound distinguishes "no outstanding pending update" from a
// validation failure so the caller can return 404 rather than redisplaying the form.
public sealed record PendingParticipantUpdateDecisionResult(
    bool Success,
    bool NotFound,
    IReadOnlyDictionary<string, string[]> FieldErrors);
