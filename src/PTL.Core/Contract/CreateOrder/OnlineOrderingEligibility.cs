using PTL.Core.Customer;
using PTL.Core.Participant;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Core.Contract.CreateOrder;

// Legacy UserPermissions.GetPermissions: online ordering needs the customer's CanOrderOnline flag AND
// the signed-in participant to be the customer's only active participant.
internal static class OnlineOrderingEligibility
{
    public static async Task<bool> CanOrderOnlineAsync(
        IParticipantRepository participantRepository,
        CoreParticipant participant,
        CoreCustomer? customer,
        CancellationToken cancellationToken)
    {
        if (customer is null || !customer.CanOrderOnline)
        {
            return false;
        }

        return await SoleActiveParticipant.IsAsync(participantRepository, participant, cancellationToken);
    }
}
