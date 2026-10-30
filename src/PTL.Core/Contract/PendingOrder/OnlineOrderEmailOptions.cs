namespace PTL.Core.Contract.PendingOrder;

// Replaces the legacy Windows service's SendOnlineContractOrderEmail switch.
public sealed class OnlineOrderEmailOptions
{
    public const string SectionName = "OnlineOrderEmails";

    public bool Enabled { get; set; } = true;
}

// Fixed - the same GOV.UK Notify templates are used in every environment, so they are not configuration.
public static class OnlineOrderEmailTemplates
{
    public const string ApprovedName = "online-contract-order-approved";
    public const string ApprovedId = "d5cb8bb4-2758-4301-b2c2-2dfe420a09d6";

    public const string DeclinedName = "online-contract-order-declined";
    public const string DeclinedId = "e5ceb263-94e2-44f8-af8f-480981f18978";
}
