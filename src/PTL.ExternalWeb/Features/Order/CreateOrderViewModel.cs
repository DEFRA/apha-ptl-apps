using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PTL.Contracts.CreateOrder;

namespace PTL.ExternalWeb.Features.Order;

// View model for the Create New Order page (legacy PendingContractOrder.aspx stage 1): the scheme grid for one
// contract year, with the commercial list optionally filtered by the Search box.
public sealed class CreateOrderViewModel
{
    public required string YearSlug { get; init; }

    public required CreateOrderResponse Order { get; init; }

    // The term the commercial list is currently filtered by; null/blank shows every scheme (legacy "Show All").
    public string? Search { get; init; }

    // Target of the "website" link in the intro; null shows the word as plain text.
    public string? DistributionTimetableUrl { get; init; }

    // Per-table sort choices carried in the address (legacy kept them in its page state).
    public OrderSortState Sort { get; init; } = OrderSortState.Empty;

    public bool IsCurrentYear => YearSlug == OrderController.CurrentYearSlug;

    public string PageAction => IsCurrentYear ? nameof(OrderController.CurrentYear) : nameof(OrderController.NextYear);

    public IReadOnlyList<CreateOrderSchemeResponse> ParticipatingSchemes => Sort.Apply(OrderSortState.ParticipatingTable, Order.ParticipatingSchemes);

    public IReadOnlyList<CreateOrderScheduleGroupResponse> VisibleGroups =>
    [
        .. CreateOrderSearch.Filter(Order.AvailableSchemes, Search)
            .Select(g => g with { Schemes = Sort.Apply(OrderSortState.ScheduleTable(g.Schedule), g.Schemes) })
    ];

    public string FormatMoney(decimal amount) => Format(Order.CurrencySymbol, amount);

    public static string Format(string symbol, decimal amount) => symbol + amount.ToString("N2", CultureInfo.InvariantCulture);
}

public static class CreateOrderSearch
{
    // Legacy Match(): every space-separated word must appear in "name identifier - (schedule)", ignoring case.
    public static IReadOnlyList<CreateOrderScheduleGroupResponse> Filter(IReadOnlyList<CreateOrderScheduleGroupResponse> groups, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return groups;
        }

        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return groups
            .Select(g => new CreateOrderScheduleGroupResponse(
                g.Schedule,
                g.Schemes.Where(s => Matches(s, words)).ToList()))
            .Where(g => g.Schemes.Count > 0)
            .ToList();
    }

    private static bool Matches(CreateOrderSchemeResponse scheme, string[] words)
    {
        var text = $"{scheme.Name} {scheme.Identifier} - ({scheme.Schedule})";
        return words.All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}

// Form posted by create-order.js for each checkbox change.
public sealed class CreateOrderChangeModel
{
    public string? Year { get; set; }

    [BindRequired]
    public Guid SchemeId { get; set; }

    public string? Field { get; set; }

    [BindRequired]
    public bool Checked { get; set; }

    public string? Search { get; set; }

    // Posted by create-order.js so the re-priced body keeps the sort the page was loaded with.
    public List<string> Sort { get; set; } = [];
}

public sealed record CreateOrderReviewViewModel(string YearSlug, CreateOrderResponse Order)
{
    public string BackAction => YearSlug == OrderController.CurrentYearSlug
        ? nameof(OrderController.CurrentYear)
        : nameof(OrderController.NextYear);

    // Legacy PurchaseOrderNumberTextbox / CheckBoxTermsAndConditions / consent checkboxes: the saved values on a first visit,
    // the posted values when the page is shown again with errors.
    public string PurchaseOrderNumber { get; init; } = Order.PurchaseOrderNumber;

    public bool TermsAccepted { get; init; }

    public IReadOnlyCollection<Guid> ConsentedSchemeIds { get; init; } = [.. Order.DataConsents.Where(c => c.Given).Select(c => c.SchemeId)];

    public IReadOnlyList<CreateOrderValidationErrorResponse> Errors { get; init; } = [];

    public string? TermsUrl { get; init; }

    // Target of the price-list "website" link on the printout; null shows the word as plain text.
    public string? DistributionTimetableUrl { get; init; }

    // Pending-order table sort; the summary starts on scheme name ascending, as in legacy.
    public OrderSortState Sort { get; init; } = OrderSortState.Empty;

    public bool HasConsents => Order.DataConsents.Count > 0;

    public bool IsConsented(Guid schemeId) => ConsentedSchemeIds.Contains(schemeId);

    public string? ErrorFor(string field) => Errors.FirstOrDefault(e => e.Field == field)?.Message;

    // Field order follows the page, top to bottom.
    public IReadOnlyList<(string Field, string Message)> ErrorSummary =>
        [.. Errors.OrderBy(e => Array.IndexOf(ErrorFieldOrder, e.Field)).Select(e => (e.Field, e.Message))];

    private static readonly string[] ErrorFieldOrder = ["Schemes", "PurchaseOrderNumber", "DataConsent", "TermsAccepted"];

    // Schemes in the pending order: selected, with at least one pending month (legacy GetPendingList).
    public IReadOnlyList<CreateOrderSchemeResponse> Schemes => Sort.Apply(
        OrderSortState.SummaryTable,
        [.. Order.ParticipatingSchemes
            .Concat(Order.AvailableSchemes.SelectMany(g => g.Schemes))
            .Where(s => s.Selected && s.HasPendingMonths)]);

    public string FormatMoney(decimal amount) => CreateOrderViewModel.Format(Order.CurrencySymbol, amount);

    // Legacy's Order details block put the currency in the label and showed the bare amount.
    public static string FormatAmount(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
}

// Form posted by the Pending order page's Save and Submit buttons.
public sealed class CreateOrderReviewForm
{
    public string? Year { get; set; }

    public string? PurchaseOrderNumber { get; set; }

    // Nullable so a missing field is told apart from an unticked box; both mean the terms were not accepted.
    public bool? TermsAccepted { get; set; }

    public List<Guid> ConsentedSchemeIds { get; set; } = [];

    // The Pending order table's sort when the form was posted, so a failed submit keeps it.
    public List<string> Sort { get; set; } = [];
}
