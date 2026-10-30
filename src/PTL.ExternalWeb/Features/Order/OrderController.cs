using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.CreateOrder;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Order;

// Legacy PendingContractOrder.aspx stage 1 (tracker row 12, Story 744642): a participant picks schemes and
// distribution months for the current or next contract year and sees live pricing. The order is a server-side
// draft that PTL.Api creates, prices and persists on every change, exactly as legacy did.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class OrderController(ICreateOrderApiClient createOrderApiClient) : Controller
{
    public const string CurrentYearSlug = "current";
    public const string NextYearSlug = "next";

    private const string BodyPartial = "_OrderBody";
    private static readonly HashSet<string> MonthFields = new(["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"], StringComparer.Ordinal);

    // "Orders" used to land here; the section now has one page per contract year, so send anyone with an old link Home.
    public IActionResult Index() => RedirectToAction("Index", "Home");

    [HttpGet]
    public Task<IActionResult> CurrentYear(string? search, CancellationToken cancellationToken) =>
        ShowAsync(CurrentYearSlug, search, cancellationToken);

    [HttpGet]
    public Task<IActionResult> NextYear(string? search, CancellationToken cancellationToken) =>
        ShowAsync(NextYearSlug, search, cancellationToken);

    // Called by create-order.js on every checkbox change: applies it to the draft and returns the re-priced order body.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(CreateOrderChangeModel model, CancellationToken cancellationToken)
    {
        var year = NormaliseYear(model.Year);
        var field = model.Field?.Trim().ToLowerInvariant();
        if (!ModelState.IsValid || year is null || field is null || (field != "select" && field != "importexport" && !MonthFields.Contains(field)))
        {
            return BadRequest();
        }

        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var result = await createOrderApiClient.ChangeAsync(
            participantId.Value, year, new CreateOrderLineChangeRequest(model.SchemeId, field, model.Checked), cancellationToken);

        return result.Status switch
        {
            CreateOrderApiStatus.Ok => PartialView(BodyPartial, new CreateOrderViewModel { YearSlug = year, Order = result.Order!, Search = model.Search }),
            CreateOrderApiStatus.NotAllowed => StatusCode(StatusCodes.Status403Forbidden),
            _ => NotFound()
        };
    }

    // Legacy "Clear Form" opens a Yes/No dialog first; this is its confirmation page.
    [HttpGet]
    public IActionResult ConfirmClear(string? year)
    {
        var slug = NormaliseYear(year);
        return slug is null ? NotFound() : View(new CreateOrderConfirmClearViewModel(slug));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(string? year, CancellationToken cancellationToken)
    {
        var slug = NormaliseYear(year);
        var participantId = User.GetParticipantId();
        if (slug is null || participantId is null)
        {
            return NotFound();
        }

        return await createOrderApiClient.ClearAsync(participantId.Value, slug, cancellationToken) switch
        {
            CreateOrderApiStatus.Ok => RedirectToAction(PageAction(slug)),
            CreateOrderApiStatus.NotAllowed => NotAvailable(slug),
            _ => NotFound()
        };
    }

    // Legacy "Next": stage 2 of the wizard. This release shows the pending order read-only; entering a purchase order
    // number, accepting the terms and submitting are not part of this stage.
    [HttpGet]
    public async Task<IActionResult> Review(string? year, CancellationToken cancellationToken)
    {
        var slug = NormaliseYear(year);
        var participantId = User.GetParticipantId();
        if (slug is null || participantId is null)
        {
            return NotFound();
        }

        var result = await createOrderApiClient.GetAsync(participantId.Value, slug, cancellationToken);
        if (result.Status != CreateOrderApiStatus.Ok)
        {
            return result.Status == CreateOrderApiStatus.NotAllowed ? NotAvailable(slug) : NotFound();
        }

        // Legacy only enables Next once a scheme with a month is selected.
        return result.Order!.CanProceed
            ? View(new CreateOrderReviewViewModel(slug, result.Order))
            : RedirectToAction(PageAction(slug));
    }

    private async Task<IActionResult> ShowAsync(string slug, string? search, CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var result = await createOrderApiClient.GetAsync(participantId.Value, slug, cancellationToken);
        return result.Status switch
        {
            CreateOrderApiStatus.Ok => View("Index", new CreateOrderViewModel { YearSlug = slug, Order = result.Order!, Search = search }),
            CreateOrderApiStatus.NotAllowed => NotAvailable(slug),
            _ => NotFound()
        };
    }

    // Legacy threw "insufficient page permissions" when the participant could not order online or had already
    // submitted an order for the year; this explains it instead.
    private ViewResult NotAvailable(string slug)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("NotAvailable", slug);
    }

    private static string PageAction(string slug) => slug == CurrentYearSlug ? nameof(CurrentYear) : nameof(NextYear);

    private static string? NormaliseYear(string? year) => year?.Trim().ToLowerInvariant() switch
    {
        CurrentYearSlug => CurrentYearSlug,
        NextYearSlug => NextYearSlug,
        _ => null
    };
}

public sealed record CreateOrderConfirmClearViewModel(string YearSlug);
