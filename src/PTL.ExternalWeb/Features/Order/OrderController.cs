using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.ApiClient;
using PTL.Contracts.CreateOrder;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Features.Order;

// Legacy PendingContractOrder.aspx (tracker row 12, Stories 744642 and 744643): a participant picks schemes and
// distribution months for the current or next contract year and sees live pricing (stage 1), then reviews the pending
// order, enters a purchase order number, accepts the terms and submits it (stage 2). The order is a server-side
// draft that PTL.Api creates, prices and persists on every change, exactly as legacy did.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class OrderController(ICreateOrderApiClient createOrderApiClient, IOptions<OrderPageOptions> pageOptions) : Controller
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
            CreateOrderApiStatus.Ok => PartialView(BodyPartial, new CreateOrderViewModel
            {
                YearSlug = year,
                Order = result.Order!,
                Search = model.Search,
                Sort = OrderSortState.Parse(model.Sort)
            }),
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

    // Legacy "Next": stage 2 of the wizard - the pending order with the purchase order number, the terms and Submit.
    [HttpGet]
    public async Task<IActionResult> Review(string? year, CancellationToken cancellationToken)
    {
        var review = await GetReviewModelAsync(year, includeConsents: true, Request.Query["sort"], cancellationToken);
        return review.Error ?? View(review.Model!);
    }

    // Legacy BtnSave_Click: keeps the purchase order number on the draft and stays on the page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CreateOrderReviewForm form, CancellationToken cancellationToken)
    {
        var slug = NormaliseYear(form.Year);
        var participantId = User.GetParticipantId();
        if (slug is null || participantId is null)
        {
            return NotFound();
        }

        var result = await createOrderApiClient.SavePurchaseOrderAsync(
            participantId.Value, slug, new CreateOrderSaveRequest(form.PurchaseOrderNumber), cancellationToken);
        if (result.Status != CreateOrderApiStatus.Ok)
        {
            return result.Status == CreateOrderApiStatus.NotAllowed ? NotAvailable(slug) : NotFound();
        }

        if (result.Errors.Count > 0)
        {
            return await ReviewWithErrorsAsync(slug, form, result.Errors, cancellationToken);
        }

        TempData.SetNotification(NotificationType.Success, "Your pending order has been saved.");
        return RedirectToAction(nameof(Review), new { year = slug, sort = form.Sort });
    }

    // Legacy BtnSubmit_Click: the order is only submitted once the terms (and any data consent) are accepted.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(CreateOrderReviewForm form, CancellationToken cancellationToken)
    {
        var slug = NormaliseYear(form.Year);
        var participantId = User.GetParticipantId();
        if (slug is null || participantId is null)
        {
            return NotFound();
        }

        var result = await createOrderApiClient.SubmitAsync(
            participantId.Value, slug, new CreateOrderSubmitRequest(form.PurchaseOrderNumber, form.TermsAccepted, form.ConsentedSchemeIds), cancellationToken);
        if (result.Status != CreateOrderApiStatus.Ok)
        {
            return result.Status == CreateOrderApiStatus.NotAllowed ? NotAvailable(slug) : NotFound();
        }

        if (result.Errors.Count > 0)
        {
            return await ReviewWithErrorsAsync(slug, form, result.Errors, cancellationToken);
        }

        TempData.SetNotification(NotificationType.Success, "Your order has been submitted and is waiting for review.");
        return RedirectToAction("Index", "Home");
    }

    // Req3 print-only slice: printable pending-order summary before submission, with the same access
    // and selection guard as Review.
    [HttpGet]
    public async Task<IActionResult> Print(string? year, CancellationToken cancellationToken)
    {
        var review = await GetReviewModelAsync(year, includeConsents: true, Request.Query["sort"], cancellationToken);
        return review.Error ?? View("Print", review.Model!);
    }

    private async Task<IActionResult> ReviewWithErrorsAsync(
        string slug, CreateOrderReviewForm form, IReadOnlyList<CreateOrderValidationErrorResponse> errors, CancellationToken cancellationToken)
    {
        var review = await GetReviewModelAsync(slug, includeConsents: true, form.Sort, cancellationToken);
        if (review.Error is not null)
        {
            return review.Error;
        }

        return View("Review", review.Model! with
        {
            PurchaseOrderNumber = form.PurchaseOrderNumber ?? string.Empty,
            TermsAccepted = form.TermsAccepted,
            ConsentedSchemeIds = form.ConsentedSchemeIds,
            Errors = errors
        });
    }

    private async Task<(IActionResult? Error, CreateOrderReviewViewModel? Model)> GetReviewModelAsync(
        string? year, bool includeConsents, IEnumerable<string?>? sort, CancellationToken cancellationToken)
    {
        var slug = NormaliseYear(year);
        var participantId = User.GetParticipantId();
        if (slug is null || participantId is null)
        {
            return (NotFound(), null);
        }

        var result = await createOrderApiClient.GetAsync(participantId.Value, slug, includeConsents, cancellationToken);
        if (result.Status != CreateOrderApiStatus.Ok)
        {
            return (result.Status == CreateOrderApiStatus.NotAllowed ? NotAvailable(slug) : NotFound(), null);
        }

        // Legacy only enables Next once a scheme with a month is selected.
        if (!result.Order!.CanProceed)
        {
            TempData.SetNotification(NotificationType.Error, "Select at least one scheme and at least one distribution month for it before you continue.");
            return (RedirectToAction(PageAction(slug)), null);
        }

        return (null, new CreateOrderReviewViewModel(slug, result.Order)
        {
            TermsUrl = pageOptions.Value.SafeTermsAndConditionsUrl,
            DistributionTimetableUrl = pageOptions.Value.SafeDistributionTimetableUrl,
            Sort = OrderSortState.Parse(sort)
        });
    }

    private async Task<IActionResult> ShowAsync(string slug, string? search, CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var result = await createOrderApiClient.GetAsync(participantId.Value, slug, cancellationToken: cancellationToken);
        return result.Status switch
        {
            CreateOrderApiStatus.Ok => View("Index", new CreateOrderViewModel
            {
                YearSlug = slug,
                Order = result.Order!,
                Search = search,
                Sort = OrderSortState.Parse(Request.Query["sort"]),
                DistributionTimetableUrl = pageOptions.Value.SafeDistributionTimetableUrl
            }),
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
