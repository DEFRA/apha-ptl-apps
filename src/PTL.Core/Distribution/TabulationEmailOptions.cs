namespace PTL.Core.Distribution;

public sealed class TabulationEmailOptions
{
    public const string SectionName = "TabulationEmails";

    public bool Enabled { get; set; } = true;

    // The ready date and "today" are calendar days in this zone.
    public string TimeZone { get; set; } = "Europe/London";

    // Paths of the comment pages linked from the Test Consultant emails (legacy Test Consultant/Tabulation.aspx
    // and CommentsEntry.aspx). The internal page is migrated (PTL.InternalWeb TestConsultantController); the external one is not yet.
    public string InternalCommentsPath { get; set; } = "Test%20Consultant/Tabulation";

    public string ExternalCommentsPath { get; set; } = "CommentsEntry";
}

public sealed class PublicationNotificationOptions
{
    public const string SectionName = "PublicationNotifications";

    // Off until the publish workflow exists to queue tblNotificationEmail rows.
    public bool Enabled { get; set; }
}

// Fixed - the same GOV.UK Notify templates are used in every environment, so they are not configuration.
public static class TabulationEmailTemplates
{
    public const string FirstRequestName = "tabulation-tc-first-request";
    public const string FirstRequestId = "a2da705b-46b7-47cc-9ec7-00d5eec94f2d";

    public const string ChaserName = "tabulation-tc-chaser";
    public const string ChaserId = "ca746cae-e71d-47f9-a263-f246e8d60edc";

    public const string PublishedName = "tabulation-published-notification";
    public const string PublishedId = "396b4c10-e36c-462e-9629-ea9d1950ef3e";
}

// Days counted from D, the date the distribution was sent to the Test Consultant (legacy email.vb used 7 and 9).
public static class TabulationEmailSchedule
{
    public const int FirstEmailLastDay = 7;

    public const int ChaserFirstDay = 8;

    // The chaser is due while today is before D+10; from D+10 the comments are OVERDUE.
    public const int OverdueDay = 10;

    public const int FirstEmailDeadlineDays = 7;

    public const int ChaserDeadlineDays = 2;
}
