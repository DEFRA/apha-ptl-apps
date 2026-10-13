namespace PTL.Core.TestConsultant;

// tblExternalTestConsultant - external subject-matter-expert/assessor role. Originally just
// enough to support CIDM external-user resolution; now also backs the External Test Consultant
// Management admin screen (add/edit/status + the stubbed Generate Login action).
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
