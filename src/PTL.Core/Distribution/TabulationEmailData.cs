namespace PTL.Core.Distribution;

public enum TabulationEmailType
{
    FirstRequest,
    Chaser,
    PublishedNotification,
}

// A distribution sent to the Test Consultant within the chaser window - the columns of legacy
// spgaMonthlyDistributionSchemeForEmail plus the scheme's Test Consultant ids.
public sealed class TabulationEmailCandidateEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public Guid SchemeId { get; set; }
    public string SchemeName { get; set; } = string.Empty;
    public string DistributionReference { get; set; } = string.Empty;
    public string DistributionReferenceSuffix { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public int EmailState { get; set; }
    public bool IsCommentSubmitted { get; set; }
    public DateTime IsReadyForTestConsultant { get; set; }
    public Guid? TestConsultant1Id { get; set; }
    public Guid? TestConsultant2Id { get; set; }
    public Guid? TestConsultant3Id { get; set; }

    // Legacy MonthlyDistributionReferenceFull.
    public string ReferenceFull => $"{DistributionReference}{DistributionReferenceSuffix}/{ScheduleCode}";
}

// A row of spgaUserAllTestConsultant (internal users holding the role, then external consultants).
public sealed class TabulationConsultantEntity
{
    public Guid UserId { get; set; }
    public string FriendlyName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Email { get; set; }

    // Set by the repository from which result set the row came.
    public bool IsExternal { get; set; }
}

// A row of spgaNotificationEmail - one unsent publication notification.
public sealed class NotificationEmailEntity
{
    public Guid NotificationEmailId { get; set; }
    public Guid PublishedTabulationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Distribution { get; set; } = string.Empty;

    // "{scheme identifier}: {scheme name}".
    public string Scheme { get; set; } = string.Empty;
}

public sealed record TabulationEmailRunSummary(int Due, int FirstEmailsSent, int ChaserEmailsSent, int Skipped, int Failed)
{
    public static TabulationEmailRunSummary Empty { get; } = new(0, 0, 0, 0, 0);
}

public sealed record PublicationNotificationRunSummary(int Unsent, int Sent, int Skipped, int Failed)
{
    public static PublicationNotificationRunSummary Empty { get; } = new(0, 0, 0, 0);
}
