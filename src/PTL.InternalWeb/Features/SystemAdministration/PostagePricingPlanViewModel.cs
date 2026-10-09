using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class PostagePricingPlanViewModel
{
    public int? SelectedYearId { get; set; }

    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    public List<PostagePricingPlanRowViewModel> Rows { get; set; } = [];

    // True only when there is no financial year with any postage pricing plan configured at all -
    // mirrors legacy PostagePricingPlanPage.Page_Load's "Please contact SFW" fallback.
    public bool HasNoYears { get; set; }

    public bool CanRenew { get; set; }

    public string? NextYearLabel { get; set; }

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}

public sealed class PostagePricingPlanRowViewModel
{
    public Guid PostageId { get; set; }

    public int YearId { get; set; }

    public string Name { get; set; } = string.Empty;

    // Cosmetic only - the legacy screen displays "Dry Ice" even though the stored fldName value is
    // "Dryice" (no space); the underlying data is left untouched.
    public string DisplayName => Name.Equals("Dryice", StringComparison.OrdinalIgnoreCase) ? "Dry Ice" : Name;

    // Only one row is ever in edit mode at a time - toggled via the editId query/route value.
    public bool IsEditing { get; set; }

    public decimal UKPrice { get; set; }

    public decimal EUPrice { get; set; }

    public decimal NonEUPrice { get; set; }
}

// Posted by the single "Save" button against whichever row is in edit mode - matches legacy
// GridViewPostagePricingPlan_Updating, which only ever updates the one row with EditIndex set.
public sealed class PostagePricingPlanEditViewModel
{
    [Required]
    public Guid? PostageId { get; set; }

    // Page-navigation state only (which year to redisplay/redirect to) - never sent to the price
    // update API, which always sources the row's real year server-side.
    [Required]
    public int? YearId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "A UK Price is required and must not be negative")]
    public decimal UKPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "A EU Price is required and must not be negative")]
    public decimal EUPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "A Non EU Price is required and must not be negative")]
    public decimal NonEUPrice { get; set; }
}
