namespace PTL.Core.Customer;

public interface ICustomerDetailsEditService
{
    // Returns null when the participant doesn't resolve to a customer at all.
    Task<CustomerDetailsEditState?> GetAsync(Guid participantId, CancellationToken cancellationToken = default);

    // Throws CustomerValidationException when business rules are violated, and
    // PendingCustomerUpdateAlreadySubmittedException when an outstanding pending update already
    // exists (legacy disables the whole form in this state - see EditCustomerDetails.aspx.vb).
    Task SubmitAsync(Guid participantId, PendingCustomerUpdate proposed, CancellationToken cancellationToken = default);
}

// The form's current values (pending update's values when one is outstanding, else the live
// customer's) plus the two pieces of read-only chrome the page needs (QAL number, whether the
// form should be shown as disabled-with-a-notice).
public sealed record CustomerDetailsEditState(string QalNumber, bool HasOutstandingUpdate, PendingCustomerUpdate Values);

// Thrown by CustomerDetailsEditService.SubmitAsync when a submitted-but-undecided pending update
// already exists for this customer - legacy disables the form entirely rather than allowing a
// second submission to overwrite the first before an administrator has reviewed it.
public sealed class PendingCustomerUpdateAlreadySubmittedException() : Exception(
    "Changes have already been submitted. Please wait for those changes to be approved or rejected before submitting further changes.");
