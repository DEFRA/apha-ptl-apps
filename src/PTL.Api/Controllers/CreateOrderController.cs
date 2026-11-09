using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.CreateOrder;
using PTL.Core.Contract.CreateOrder;

namespace PTL.Api.Controllers;

// Participant-scoped self-service endpoint (legacy PendingContractOrder.aspx stage 1). ParticipantId identifies
// the signed-in participant; the customer is always resolved server-side, never accepted from the caller
// (see docs/ADR/0006-externalweb-participant-scoped-authorization-model.md). An ineligible participant gets 403.
[ApiController]
[Route("api/create-order")]
public sealed class CreateOrderController(ICreateOrderService createOrderService) : ControllerBase
{
    [HttpGet("{participantId:guid}/eligibility")]
    public async Task<ActionResult<CreateOrderEligibilityResponse>> GetEligibility(Guid participantId, CancellationToken cancellationToken)
    {
        var eligibility = await createOrderService.GetEligibilityAsync(participantId, cancellationToken);
        return eligibility is null
            ? NotFound()
            : Ok(new CreateOrderEligibilityResponse(
                eligibility.CanOrderOnline, eligibility.CanCreateCurrentYearOrder, eligibility.CanCreateNextYearOrder, eligibility.CurrentYear, eligibility.NextYear));
    }

    [HttpGet("{participantId:guid}/{year}")]
    public async Task<ActionResult<CreateOrderResponse>> Get(Guid participantId, OrderYear year, [FromQuery] bool review, CancellationToken cancellationToken) =>
        ToActionResult(await createOrderService.GetOrderAsync(participantId, year, review, cancellationToken));

    [HttpPost("{participantId:guid}/{year}/change")]
    public async Task<ActionResult<CreateOrderResponse>> Change(
        Guid participantId, OrderYear year, [FromBody] CreateOrderLineChangeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ToActionResult(await createOrderService.ApplyChangeAsync(
                participantId, year, new CreateOrderLineChange(request.SchemeId, request.Field, request.Checked), cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{participantId:guid}/{year}/clear")]
    public async Task<IActionResult> Clear(Guid participantId, OrderYear year, CancellationToken cancellationToken) =>
        await createOrderService.ClearAsync(participantId, year, cancellationToken) switch
        {
            CreateOrderAccess.Allowed => NoContent(),
            CreateOrderAccess.NotEligible => StatusCode(StatusCodes.Status403Forbidden),
            _ => NotFound()
        };

    // Legacy stage 2 Save: 204 when stored, 400 with the field errors when the purchase order number is too long.
    [HttpPost("{participantId:guid}/{year}/purchase-order")]
    public async Task<IActionResult> SavePurchaseOrder(Guid participantId, OrderYear year, [FromBody] CreateOrderSaveRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await createOrderService.SavePurchaseOrderAsync(participantId, year, request.PurchaseOrderNumber, cancellationToken));

    // Legacy stage 2 Submit: 204 when the order is submitted, 400 with the field errors when a rule fails.
    [HttpPost("{participantId:guid}/{year}/submit")]
    public async Task<IActionResult> Submit(Guid participantId, OrderYear year, [FromBody] CreateOrderSubmitRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await createOrderService.SubmitAsync(
            participantId, year, new CreateOrderSubmission(request.PurchaseOrderNumber, request.TermsAccepted, request.ConsentedSchemeIds ?? []), cancellationToken));

    private IActionResult ToActionResult(CreateOrderSubmitOutcome outcome) => outcome.Access switch
    {
        CreateOrderAccess.Allowed when outcome.Errors.Count == 0 => NoContent(),
        CreateOrderAccess.Allowed => BadRequest(new CreateOrderValidationResponse([.. outcome.Errors.Select(e => new CreateOrderValidationErrorResponse(e.Field, e.Message))])),
        CreateOrderAccess.NotEligible => StatusCode(StatusCodes.Status403Forbidden),
        _ => NotFound()
    };

    private ActionResult<CreateOrderResponse> ToActionResult(CreateOrderResult result) => result.Access switch
    {
        CreateOrderAccess.Allowed => Ok(ToResponse(result.Page!)),
        CreateOrderAccess.NotEligible => StatusCode(StatusCodes.Status403Forbidden),
        _ => NotFound()
    };

    private static CreateOrderResponse ToResponse(CreateOrderPage page) => new(
        page.Year,
        page.CurrencySymbol,
        page.QalNumber,
        page.LabCode,
        page.IsAphaCustomer,
        page.ParticipatingSchemes.Select(ToResponse).ToList(),
        page.AvailableSchemes.Select(g => new CreateOrderScheduleGroupResponse(g.Schedule, g.Schemes.Select(ToResponse).ToList())).ToList(),
        page.SchemeTotal,
        page.PostageTotal,
        page.OrderTotal,
        page.CanProceed,
        page.PurchaseOrderNumber,
        page.Consents?.Select(c => new CreateOrderConsentResponse(c.SchemeId, c.Identifier, c.Name, c.Text, c.Given)).ToList());

    private static CreateOrderSchemeResponse ToResponse(CreateOrderScheme scheme) => new(
        scheme.SchemeId,
        scheme.Identifier,
        scheme.Name,
        scheme.Schedule,
        scheme.AnnualPrice,
        scheme.Selected,
        scheme.ImportPermitRequired,
        scheme.ImportPermitEnabled,
        scheme.PostagePrice,
        scheme.SchemePrice,
        scheme.TotalPrice,
        scheme.HasPendingMonths,
        scheme.Months.Select(m => new CreateOrderMonthResponse(m.Label, m.MonthNumber, m.Offered, m.Selected, m.Enabled, m.AlreadyParticipating)).ToList());
}
