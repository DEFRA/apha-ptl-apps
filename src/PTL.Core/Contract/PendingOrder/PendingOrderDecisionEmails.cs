using PTL.Core.Participant;
using ICustomerRepository = PTL.Core.Customer.ICustomerRepository;

namespace PTL.Core.Contract.PendingOrder;

// Queues and immediately sends the emails of a decided pending order.
public interface IPendingOrderDecisionEmails
{
    Task QueueAndSendAsync(PendingOrderEntity order, Guid contractId, string comments, bool isApproved, CancellationToken cancellationToken = default);
}

public sealed class PendingOrderDecisionEmails(
    IPendingOrderRepository pendingOrderRepository,
    ICustomerRepository customerRepository,
    IParticipantRepository participantRepository,
    IPendingOrderNotifier pendingOrderNotifier) : IPendingOrderDecisionEmails
{
    // tblOnlineContractOrderEmail.fldEmail / fldCustomerName are varchar(50).
    private const int EmailColumnLength = 50;

    // Legacy EmailOrderNotifications: the customer, the customer's first active participant and that
    // participant's second address each get a row in tblOnlineContractOrderEmail, which is then sent at
    // once (retried by the scheduler if that fails). Blank addresses are skipped rather than queued.
    public async Task QueueAndSendAsync(PendingOrderEntity order, Guid contractId, string comments, bool isApproved, CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);
        var customerName = Truncate(customer?.Name ?? order.CustomerName, EmailColumnLength);

        var recipients = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer?.Email))
        {
            recipients.Add(customer.Email);
        }

        recipients.AddRange(await ParticipantAddressesAsync(order.CustomerId, cancellationToken));

        foreach (var email in recipients)
        {
            await pendingOrderRepository.QueueNotificationAsync(
                new PendingOrderNotification(Truncate(email, EmailColumnLength), contractId, order.PendingContractId, order.YearId, customerName, comments, isApproved),
                cancellationToken);
        }

        await pendingOrderNotifier.SendForOrderAsync(order.PendingContractId, CancellationToken.None);
    }

    private async Task<List<string>> ParticipantAddressesAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var addresses = new List<string>();
        var activeParticipants = await participantRepository.GetSummariesAsync(customerId, false, cancellationToken);
        if (activeParticipants.Count == 0)
        {
            return addresses;
        }

        var participant = await participantRepository.GetByIdAsync(activeParticipants[0].ParticipantId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(participant?.Email))
        {
            addresses.Add(participant.Email);
        }

        if (!string.IsNullOrWhiteSpace(participant?.Email2))
        {
            addresses.Add(participant.Email2);
        }

        return addresses;
    }

    private static string Truncate(string value, int length) => value.Length > length ? value[..length] : value;
}
