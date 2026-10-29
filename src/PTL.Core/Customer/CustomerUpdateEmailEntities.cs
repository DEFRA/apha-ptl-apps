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

// Replaces the legacy Windows service's SendCustomerUpdateEmails switch.
public sealed class CustomerUpdateEmailOptions
{
    public const string SectionName = "CustomerUpdateEmails";

    public bool Enabled { get; set; } = true;
}

// Fixed - the same GOV.UK Notify templates are used in every environment, so they are not configuration.
public static class CustomerUpdateEmailTemplates
{
    public const string AcceptedName = "customer-update-accepted";
    public const string AcceptedId = "b74c9d08-c5bc-4401-a421-68c49701489d";

    public const string DeclinedName = "customer-update-declined";
    public const string DeclinedId = "77a17937-2e31-45ac-b2c4-be93485779cb";
}
