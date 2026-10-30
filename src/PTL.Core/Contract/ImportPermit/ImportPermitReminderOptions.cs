namespace PTL.Core.Contract.ImportPermit;

// Replaces the legacy service's SendImportPermitReminderEmails switch and its three
// SendImportPermitXxxReminderEmailDays values.
public sealed class ImportPermitReminderOptions
{
    public const string SectionName = "ImportPermitReminders";

    public bool Enabled { get; set; } = true;

    public int FirstReminderDays { get; set; } = 28;

    public int SecondReminderDays { get; set; } = 14;

    public int ThirdReminderDays { get; set; } = 7;

    public IReadOnlyList<int> Offsets => [FirstReminderDays, SecondReminderDays, ThirdReminderDays];
}

// Fixed - the same GOV.UK Notify templates are used in every environment, so they are not configuration.
public static class ImportPermitReminderTemplates
{
    public const string CustomerName = "import-permit-reminder-customer";
    public const string CustomerId = "8d8c1bd5-0531-4a80-88b1-d453d4b576d3";

    public const string ParticipantName = "import-permit-reminder-participant";
    public const string ParticipantId = "d7d8f937-6d63-44fd-a9d0-670f9efd990f";
}
