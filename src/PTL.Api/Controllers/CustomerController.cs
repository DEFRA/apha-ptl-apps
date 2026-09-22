using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.Contracts.Customer;
using PTL.Core.Customer;

namespace PTL.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomerController(ICustomerService customerService, ILogger<CustomerController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, Exception?> LogCustomerNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogCustomerNotFoundMessage)),
            "Customer {CustomerId} not found");

    // GET /api/customers?status=Active|Inactive|All - defaults to Active to match the legacy CustomerList.aspx default filter.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerSummaryResponse>>> GetCustomers(
        [FromQuery] CustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customers = await customerService.GetCustomersAsync(request.Status, cancellationToken);
        return Ok(customers.Select(ToSummaryResponse).ToList());
    }

    [HttpGet("{customerId:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetCustomer(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetCustomerAsync(customerId, cancellationToken);
        if (customer is null)
        {
            LogCustomerNotFoundMessage(logger, customerId, null);
            return NotFound();
        }

        return Ok(ToResponse(customer));
    }

    // GET /api/customers/search?searchTerm=...&status=Active|Inactive|All&page=1&pageSize=20
    [HttpGet("search")]
    public async Task<ActionResult<CustomerSearchResponse>> SearchCustomers([FromQuery] CustomerSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await customerService.SearchCustomersAsync(request.SearchTerm, request.Status, request.Page, request.PageSize, cancellationToken);
        return Ok(new CustomerSearchResponse(result.Items.Select(ToSummaryResponse).ToList(), result.TotalCount, request.Page, request.PageSize));
    }

    // [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
    // out of scope for this phase - assume the current caller is already authenticated with full
    // access to Customer functionality. Policies will be added in a later phase.
    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer([FromBody] CustomerSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await customerService.CreateCustomerAsync(ToEntity(Guid.Empty, request), cancellationToken);
            return CreatedAtAction(nameof(GetCustomer), new { customerId = created.CustomerId }, ToResponse(created));
        }
        catch (CustomerValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    // [NEEDS INVESTIGATION] see CreateCustomer note above - authorization deferred to a later phase.
    [HttpPut("{customerId:guid}")]
    public async Task<ActionResult<CustomerResponse>> UpdateCustomer(Guid customerId, [FromBody] CustomerSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await customerService.UpdateCustomerAsync(customerId, ToEntity(customerId, request), cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (CustomerValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    private ActionResult ToValidationProblem(CustomerValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static Customer ToEntity(Guid customerId, CustomerSaveRequest request) => new()
    {
        CustomerId = customerId,
        RegisteredFileNumber = request.RegisteredFileNumber,
        Name = request.Name,
        PreviousName = request.PreviousName,
        CustomerTypeId = request.CustomerTypeId,
        VatNumber = request.VatNumber,
        VatRatingId = request.VatRatingId,
        AccountNumber = request.AccountNumber,
        CustomerFinanceId = request.CustomerFinanceId,
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
        Fax = request.Fax,
        Email = request.Email,
        CurrencyId = request.CurrencyId,
        Comments = request.Comments,
        PostageArrangements = request.PostageArrangements,
        PaymentNonUK = request.PaymentNonUK,
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
        InvoiceFax = request.InvoiceFax,
        InvoiceEmail = request.InvoiceEmail,
        IsActive = request.IsActive,
        CanOrderOnline = request.CanOrderOnline,
        CustomerStatusId = request.CustomerStatusId
    };

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.CustomerId,
        customer.QalNumber,
        customer.RegisteredFileNumber,
        customer.Name,
        customer.PreviousName,
        customer.CustomerTypeId,
        customer.VatNumber,
        customer.VatRatingId,
        customer.AccountNumber,
        customer.CustomerFinanceId,
        customer.ContactName,
        customer.Organisation,
        customer.Address1,
        customer.Address2,
        customer.Address3,
        customer.Address4,
        customer.Address5,
        customer.CountryId,
        customer.Telephone,
        customer.Telephone2,
        customer.Fax,
        customer.Email,
        customer.CurrencyId,
        customer.Comments,
        customer.InitialStartDate,
        customer.PostageArrangements,
        customer.PaymentNonUK,
        customer.InvoiceName,
        customer.InvoiceOrganisation,
        customer.InvoiceAddress1,
        customer.InvoiceAddress2,
        customer.InvoiceAddress3,
        customer.InvoiceAddress4,
        customer.InvoiceAddress5,
        customer.InvoiceCountryId,
        customer.InvoiceTelephone,
        customer.InvoiceTelephone2,
        customer.InvoiceFax,
        customer.InvoiceEmail,
        customer.IsActive,
        customer.CanOrderOnline,
        customer.InactiveDate,
        customer.CustomerStatusId);

    private static CustomerSummaryResponse ToSummaryResponse(CustomerSummaryEntity customer) => new(
        customer.CustomerId, customer.QalNumber, customer.Name, customer.Organisation, customer.IsActive);
}
