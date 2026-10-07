namespace PTL.Core.TestConsultant;

// tblExternalTestConsultant - external subject-matter-expert/assessor role. Minimal slice: just
// enough to support CIDM external-user resolution, not the full admin/management surface
// Participant has.
public sealed class TestConsultant
{
    public Guid ExternalTestConsultantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid SsoId { get; set; }
    public Guid? SsoIdExt { get; set; }
    public bool IsInactive { get; set; }
    public DateTime? InactiveDate { get; set; }
}
