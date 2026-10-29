using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Customer;
using PTL.Core.Customer;

namespace PTL.Api.Controllers;

// Participant-scoped self-service endpoint (legacy EditCustomerDetails.aspx) - distinct from
// CustomerController's unrestricted admin CRUD. CustomerId is never accepted from the caller; it
// is always resolved server-side from the authenticated participantId (see docs/ADR/
// 0006-externalweb-participant-scoped-authorization-model.md).
[ApiController]
[Route("api/customer-details-edit")]
public sealed class CustomerDetailsEditController(ICustomerDetailsEditService customerDetailsEditService) : ControllerBase
{
    [HttpGet("{participantId:guid}")]
    public async Task<ActionResult<CustomerDetailsEditResponse>> Get(Guid participantId, CancellationToken cancellationToken)
    {
        var state = await customerDetailsEditService.GetAsync(participantId, cancellationToken);
        return state is null ? NotFound() : Ok(ToResponse(state));
    }

    [HttpPost("{participantId:guid}")]
    public async Task<ActionResult<CustomerDetailsEditSaveResult>> Submit(
        Guid participantId, [FromBody] CustomerDetailsEditSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await customerDetailsEditService.SubmitAsync(participantId, ToEntity(request), cancellationToken);
            return Ok(new CustomerDetailsEditSaveResult(true, new Dictionary<string, string[]>()));
        }
        catch (CustomerValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.Field)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
            return Ok(new CustomerDetailsEditSaveResult(false, errors));
        }
        catch (PendingCustomerUpdateAlreadySubmittedException ex)
        {
            return Ok(new CustomerDetailsEditSaveResult(false, new Dictionary<string, string[]> { [string.Empty] = [ex.Message] }));
        }
    }

    private static CustomerDetailsEditResponse ToResponse(CustomerDetailsEditState state) => new(
        state.QalNumber,
        state.HasOutstandingUpdate,
        state.Values.ContactName,
        state.Values.Organisation,
        state.Values.Address1,
        state.Values.Address2,
        state.Values.Address3,
        state.Values.Address4,
        state.Values.Address5,
        state.Values.CountryId,
        state.Values.Telephone,
        state.Values.Telephone2,
        state.Values.Email,
        state.Values.InvoiceName,
        state.Values.InvoiceOrganisation,
        state.Values.InvoiceAddress1,
        state.Values.InvoiceAddress2,
        state.Values.InvoiceAddress3,
        state.Values.InvoiceAddress4,
        state.Values.InvoiceAddress5,
        state.Values.InvoiceCountryId,
        state.Values.InvoiceTelephone,
        state.Values.InvoiceTelephone2,
        state.Values.InvoiceEmail);

    private static PendingCustomerUpdate ToEntity(CustomerDetailsEditSaveRequest request) => new()
    {
        ContactName = request.ContactName,
        Organisation = request.Organisation,
        Address1 = request.Address1,
        Address2 = request.Address2,
        Address3 = request.Address3,
        Address4 = request.Address4,
        Address5 = request.Address5,
        CountryId = request.CountryId,
        Telephone = request.Telephone,
        Telephone2 = request.Telephone2,
        Email = request.Email,
        InvoiceName = request.InvoiceName,
        InvoiceOrganisation = request.InvoiceOrganisation,
        InvoiceAddress1 = request.InvoiceAddress1,
        InvoiceAddress2 = request.InvoiceAddress2,
        InvoiceAddress3 = request.InvoiceAddress3,
        InvoiceAddress4 = request.InvoiceAddress4,
        InvoiceAddress5 = request.InvoiceAddress5,
        InvoiceCountryId = request.InvoiceCountryId,
        InvoiceTelephone = request.InvoiceTelephone,
        InvoiceTelephone2 = request.InvoiceTelephone2,
        InvoiceEmail = request.InvoiceEmail
    };
}
