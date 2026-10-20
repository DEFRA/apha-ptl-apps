using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.Contracts.Search;

namespace PTL.InternalWeb.Features.Search;

public enum SearchTab
{
    Customers,
    Participants,
}

// Legacy Contracts Admin/Search.aspx Customers tab. Max lengths are the legacy textbox MaxLength values;
// every field is optional and an all-empty search returns every customer, as in legacy.
public sealed class CustomerSearchForm
{
    [StringLength(10, ErrorMessage = "QAL Number must be 10 characters or fewer")]
    [Display(Name = "QAL Number")]
    public string? QalNumber { get; set; }

    [StringLength(20, ErrorMessage = "Account Number must be 20 characters or fewer")]
    [Display(Name = "Account Number")]
    public string? AccountNumber { get; set; }

    [StringLength(50, ErrorMessage = "Name must be 50 characters or fewer")]
    public string? Name { get; set; }

    [StringLength(50, ErrorMessage = "Contact Name must be 50 characters or fewer")]
    [Display(Name = "Contact Name")]
    public string? ContactName { get; set; }

    [StringLength(50, ErrorMessage = "Organisation must be 50 characters or fewer")]
    public string? Organisation { get; set; }

    // null = "- Not Specified -" (legacy posted Guid.Empty).
    public Guid? CountryId { get; set; }

    public CustomerSearchCriteriaRequest ToCriteria() => new(QalNumber, AccountNumber, Name, ContactName, Organisation, CountryId);
}

// Legacy Contracts Admin/Search.aspx Participants tab.
public sealed class ParticipantSearchForm
{
    [StringLength(8, ErrorMessage = "Lab Code must be 8 characters or fewer")]
    [Display(Name = "Lab Code")]
    public string? LabCode { get; set; }

    [StringLength(50, ErrorMessage = "Lab Name must be 50 characters or fewer")]
    [Display(Name = "Lab Name")]
    public string? LabName { get; set; }

    [StringLength(50, ErrorMessage = "Contact Name must be 50 characters or fewer")]
    [Display(Name = "Contact Name")]
    public string? ContactName { get; set; }

    public ParticipantSearchCriteriaRequest ToCriteria() => new(LabCode, LabName, ContactName);
}

// CustomerResults / ParticipantResults are null until that tab has been searched (legacy showed the
// "Search Results" panel only after Search was clicked); an empty list means "no results".
public sealed class SearchViewModel
{
    public SearchTab ActiveTab { get; init; } = SearchTab.Customers;

    public CustomerSearchForm Customer { get; init; } = new();

    public ParticipantSearchForm Participant { get; init; } = new();

    // Legacy DropDownSearchCustomerCountry: "- Not Specified -" first, then every country.
    public IReadOnlyList<SelectListItem> CountryOptions { get; init; } = [];

    public IReadOnlyList<CustomerSearchResultResponse>? CustomerResults { get; init; }

    public IReadOnlyList<ParticipantSearchResultResponse>? ParticipantResults { get; init; }
}
