namespace PTL.Core.Customer;

// Row shape returned by spgaUpdatedCustomerDetailsEmail (tblUpdatedCustomerDetailsEmail, fldDateSent null). The procedure
// also looks up the customer's QAL number and an active participant's lab code by customer name, which the email uses.
public class CustomerUpdateEmailEntity
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public Guid PendingCustomerUpdateId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string? QalNumber { get; set; }
    public string? LabCode { get; set; }
}

// One row of tblUpdatedCustomerDetailsEmail (legacy PendingCustomerUpdate.EmailUpdateNotifications).
public sealed record CustomerUpdateNotification(
    string Email,
    Guid PendingCustomerUpdateId,
    string CustomerName,
    string Notes,
    bool IsApproved);

// Replaces the legacy Windows service's SendCustomerUpdateEmails switch; its timer is now NotificationSchedulerOptions.
public sealed class CustomerUpdateEmailOptions
{
    public const string SectionName = "CustomerUpdateEmails";

    public bool Enabled { get; set; } = true;

    // GOV.UK Notify template ids (customer-update-accepted / customer-update-declined); a row stays unsent while its id is blank.
    public string AcceptedTemplateId { get; set; } = string.Empty;

    public string DeclinedTemplateId { get; set; } = string.Empty;
}
