namespace PTL.Core.Participant;

// Legacy ParticipantScheme.SetAudit(why, who): written to tblAuditParticipantScheme (spiAuditParticipantScheme)
// when an item is saved with an audit, which only the Contract Items removal does.
public sealed record ParticipantSchemeAudit(string AuditWho, string AuditWhy);
