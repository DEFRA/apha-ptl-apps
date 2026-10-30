namespace PTL.Core.Contract.ImportPermit;

// Fixed - the legacy SendImportPermitReminderEmails switch and its three day values never change, so they are not configuration.
public static class ImportPermitReminderSchedule
{
    // Days before overseas posting, in the order legacy processed them: first, second, third reminder.
    public static IReadOnlyList<int> Offsets { get; } = [28, 14, 7];
}

// Fixed - the same GOV.UK Notify templates are used in every environment, so they are not configuration.
public static class ImportPermitReminderTemplates
{
    public const string CustomerName = "import-permit-reminder-customer";
    public const string CustomerId = "8d8c1bd5-0531-4a80-88b1-d453d4b576d3";

    public const string ParticipantName = "import-permit-reminder-participant";
    public const string ParticipantId = "d7d8f937-6d63-44fd-a9d0-670f9efd990f";
}
