namespace PTL.Contracts.ExternalUser;

/// <summary>The lab a Participant or Lab User has chosen to work on, with what the web app keeps on the session for it.</summary>
public sealed record ExternalLabContext(Guid ParticipantId, string LabCode, string LabName, bool CanOrderOnline, bool CanEditDetails = false);
