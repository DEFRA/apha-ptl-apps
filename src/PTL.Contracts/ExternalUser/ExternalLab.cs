namespace PTL.Contracts.ExternalUser;

/// <summary>A lab (participant record) an external user can act for.</summary>
public sealed record ExternalLab(Guid ParticipantId, string LabCode, string LabName);
