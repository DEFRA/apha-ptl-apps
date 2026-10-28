namespace PTL.Core.Participant;

public interface IParticipantDetailsEditService
{
    // Returns null when the participant doesn't resolve at all.
    Task<ParticipantDetailsEditState?> GetAsync(Guid participantId, CancellationToken cancellationToken = default);

    // Throws ParticipantValidationException when business rules are violated, and
    // PendingParticipantUpdateAlreadySubmittedException when an outstanding pending update already
    // exists (legacy disables the whole form in this state - see EditParticipantDetails.aspx.vb).
    Task SubmitAsync(Guid participantId, PendingParticipantUpdate proposed, CancellationToken cancellationToken = default);
}

// The form's current values (pending update's values when one is outstanding, else the live
// participant's) plus the two pieces of read-only chrome the page needs (lab code, whether the
// form should be shown as disabled-with-a-notice).
public sealed record ParticipantDetailsEditState(string LabCode, bool HasOutstandingUpdate, PendingParticipantUpdate Values);

// Thrown by ParticipantDetailsEditService.SubmitAsync when a submitted-but-undecided pending
// update already exists for this participant - legacy disables the form entirely rather than
// allowing a second submission to overwrite the first before an administrator has reviewed it.
public sealed class PendingParticipantUpdateAlreadySubmittedException() : Exception(
    "Changes have already been submitted. Please wait for those changes to be approved or rejected before submitting further changes.");
