using PTL.Core.Participant;

namespace PTL.Core.Customer;

// Legacy EditCustomerDetails.aspx.vb's getPendingCustomerUpdate/ButtonUpdateDetails_Click,
// reproduced as a stateless service: resolves the participant's own customer server-side (never
// trusts a client-supplied CustomerId - see docs/ADR/0006), then mirrors the exact
// pending-update-or-live-customer precedence legacy uses to populate the form.
public sealed class CustomerDetailsEditService(
    IParticipantRepository participantRepository,
    ICustomerRepository customerRepository,
    IPendingCustomerUpdateRepository pendingCustomerUpdateRepository) : ICustomerDetailsEditService
{
    public async Task<CustomerDetailsEditState?> GetAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var customer = await customerRepository.GetByIdAsync(participant.CustomerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var pending = await pendingCustomerUpdateRepository.GetByCustomerIdAsync(participant.CustomerId, cancellationToken);
        if (pending is not null)
        {
            return new CustomerDetailsEditState(customer.QalNumber, HasOutstandingUpdate: true, pending);
        }

        var fromCustomer = new PendingCustomerUpdate
        {
            CustomerId = customer.CustomerId,
            ContactName = customer.ContactName,
            Organisation = customer.Organisation,
            Address1 = customer.Address1,
            Address2 = customer.Address2,
            Address3 = customer.Address3,
            Address4 = customer.Address4,
            Address5 = customer.Address5,
            CountryId = customer.CountryId,
            Telephone = customer.Telephone,
            Telephone2 = customer.Telephone2,
            Fax = customer.Fax,
            Email = customer.Email,
            InvoiceName = customer.InvoiceName,
            InvoiceOrganisation = customer.InvoiceOrganisation,
            InvoiceAddress1 = customer.InvoiceAddress1,
            InvoiceAddress2 = customer.InvoiceAddress2,
            InvoiceAddress3 = customer.InvoiceAddress3,
            InvoiceAddress4 = customer.InvoiceAddress4,
            InvoiceAddress5 = customer.InvoiceAddress5,
            InvoiceCountryId = customer.InvoiceCountryId,
            InvoiceTelephone = customer.InvoiceTelephone,
            InvoiceTelephone2 = customer.InvoiceTelephone2,
            InvoiceFax = customer.InvoiceFax,
            InvoiceEmail = customer.InvoiceEmail
        };

        return new CustomerDetailsEditState(customer.QalNumber, HasOutstandingUpdate: false, fromCustomer);
    }

    public async Task SubmitAsync(Guid participantId, PendingCustomerUpdate proposed, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var existingPending = await pendingCustomerUpdateRepository.GetByCustomerIdAsync(participant.CustomerId, cancellationToken);
        if (existingPending is not null)
        {
            throw new PendingCustomerUpdateAlreadySubmittedException();
        }

        // Fax/InvoiceFax aren't on the edit form (legacy parity) - carry the live values across so the
        // admin review screen's diff doesn't flag them as changed (legacy ConvertCustomerToPendingCustomerUpdate).
        var customer = await customerRepository.GetByIdAsync(participant.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {participant.CustomerId} not found.");
        proposed.Fax = customer.Fax;
        proposed.InvoiceFax = customer.InvoiceFax;

        var validation = PendingCustomerUpdateValidator.Validate(proposed);
        if (!validation.IsValid)
        {
            throw new CustomerValidationException(validation.Errors);
        }

        proposed.CustomerId = participant.CustomerId;
        proposed.PendingCustomerUpdateId = Guid.Empty;
        proposed.IsSubmitted = true;
        proposed.IsDeleted = false;

        await pendingCustomerUpdateRepository.UpsertAsync(proposed, cancellationToken);
    }
}
