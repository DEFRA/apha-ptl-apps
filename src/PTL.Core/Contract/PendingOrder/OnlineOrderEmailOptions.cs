namespace PTL.Core.Contract.PendingOrder;

// Replaces the legacy Windows service's SendOnlineContractOrderEmail switch; its timer is now NotificationSchedulerOptions.
public sealed class OnlineOrderEmailOptions
{
    public const string SectionName = "OnlineOrderEmails";

    public bool Enabled { get; set; } = true;

    // GOV.UK Notify template ids from the Notify portal (online-contract-order-approved / -declined); a row stays unsent while its id is blank.
    public string ApprovedTemplateId { get; set; } = string.Empty;

    public string DeclinedTemplateId { get; set; } = string.Empty;
}
