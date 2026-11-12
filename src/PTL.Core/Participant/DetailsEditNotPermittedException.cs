namespace PTL.Core.Participant;

/// <summary>Raised when a participant tries to edit customer or participant details but is not the customer's only active participant.</summary>
public sealed class DetailsEditNotPermittedException()
    : Exception("Only the customer's only active participant can edit these details.");
