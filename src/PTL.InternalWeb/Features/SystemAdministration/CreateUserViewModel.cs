using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class CreateUserViewModel
{
    [Required(ErrorMessage = "Enter an employee number, first name or last name")]
    public string SearchTerm { get; set; } = string.Empty;

    // Only set once a search has actually been performed - distinguishes "haven't searched yet"
    // from "searched and found nothing", matching legacy's SearchBox/SelectUser view split.
    // Never posted by the view - excluded from binding so a crafted form post can't fake it.
    [BindNever]
    public bool HasSearched { get; set; }

    public List<SelectListItem> ResultOptions { get; set; } = [];

    // Username of the chosen dropdown item. Only the username is posted: the controller looks the
    // person up in the directory again rather than trusting posted names and email addresses.
    public string? SelectedCandidate { get; set; }

    [StringLength(50, ErrorMessage = "Department must be 50 characters or fewer")]
    public string Department { get; set; } = string.Empty;

    // Search information shown under the search form (no results, too many results) or the success
    // message. Problems are reported through ModelState so they appear in the error summary.
    public string? Message { get; set; }

    // IsCreated is server-computed display state, never posted by the view -
    // excluded from binding so a crafted form post can't fake a success state.
    [BindNever]
    public bool IsCreated { get; set; }
}
