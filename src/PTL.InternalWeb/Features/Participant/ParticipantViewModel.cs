using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.InternalWeb.ValidationAttributes;

namespace PTL.InternalWeb.Features.Participant;

public sealed record ParticipantListViewModel(
    Guid CustomerId,
    string? SearchTerm,
    bool IncludeInactive,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<PTL.Contracts.Participant.ParticipantSummaryResponse> Participants);

// Wraps ParticipantResponse with the lookup names (LabType/Country) Details.cshtml needs but
// ParticipantResponse only carries as raw GUIDs - populated by ParticipantController.Details.
public sealed record ParticipantDetailsViewModel(
    PTL.Contracts.Participant.ParticipantResponse Participant,
    string LabTypeName,
    string CountryName);

// Review Pending Participant Updates list (legacy ReviewPendingParticipantUpdates.aspx).
public sealed record PendingParticipantUpdateListViewModel(IReadOnlyList<PTL.Contracts.Participant.PendingParticipantUpdateSummaryResponse> Updates);

// One aligned comparison row - the label appears once and both values sit on the same line,
// replacing legacy's "editable pending value with the current value in green beside it".
public sealed record PendingParticipantUpdateComparisonRow(string Label, string CurrentValue, string PendingValue)
{
    public bool HasChanged => !string.Equals(CurrentValue?.Trim(), PendingValue?.Trim(), StringComparison.Ordinal);
}

// Pending Participant Update Details comparison page (legacy PendingParticipantUpdateDetails.aspx).
public sealed record PendingParticipantUpdateDetailsViewModel(
    Guid ParticipantId,
    string LabCode,
    string LabName,
    IReadOnlyList<PendingParticipantUpdateComparisonRow> ParticipantDetails);

// Edit Pending Participant Update (legacy PendingParticipantUpdateDetails.aspx's editable form).
// Labels, field order, lengths and character rules mirror that page's LoadLabelNames() exactly.
public sealed class PendingParticipantUpdateFormViewModel
{
    public Guid ParticipantId { get; set; }

    public string? LabCode { get; set; }

    public string? LabName { get; set; }

    [Required(ErrorMessage = "Enter a contact name")]
    [StringLength(50, ErrorMessage = "Contact Name must not exceed 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_%&().',/\s\-]*$", ErrorMessage = "Contact Name contains characters that are not allowed")]
    public string? ContactName { get; set; }

    [Required(ErrorMessage = "Enter an organisation name")]
    [StringLength(50, ErrorMessage = "Organisation Name must not exceed 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_%&().',/\s\-]*$", ErrorMessage = "Organisation Name contains characters that are not allowed")]
    public string? Organisation { get; set; }

    [Required(ErrorMessage = "Enter address 1")]
    [StringLength(100, ErrorMessage = "Address 1 must not exceed 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_%&().',/\s\-]*$", ErrorMessage = "Address 1 contains characters that are not allowed")]
    public string? Address1 { get; set; }

    [Required(ErrorMessage = "Enter address 2")]
    [StringLength(100, ErrorMessage = "Address 2 must not exceed 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_%&().',/\s\-]*$", ErrorMessage = "Address 2 contains characters that are not allowed")]
    public string? Address2 { get; set; }

    [StringLength(100, ErrorMessage = "Address 3 must not exceed 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_%&().',/\s\-]*$", ErrorMessage = "Address 3 contains characters that are not allowed")]
    public string? Address3 { get; set; }

    [StringLength(100, ErrorMessage = "Address 4 must not exceed 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_%&().',/\s\-]*$", ErrorMessage = "Address 4 contains characters that are not allowed")]
    public string? Address4 { get; set; }

    [StringLength(100, ErrorMessage = "Address 5 must not exceed 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_%&().',/\s\-]*$", ErrorMessage = "Address 5 contains characters that are not allowed")]
    public string? Address5 { get; set; }

    [NotEmptyGuid(ErrorMessage = "Select a country")]
    public Guid? CountryId { get; set; }

    public IEnumerable<SelectListItem> CountryOptions { get; set; } = [];

    [Required(ErrorMessage = "Enter a telephone number")]
    [StringLength(20, ErrorMessage = "Telephone must not exceed 20 characters")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Telephone contains characters that are not allowed")]
    public string? Telephone { get; set; }

    [StringLength(20, ErrorMessage = "Fax must not exceed 20 characters")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Fax contains characters that are not allowed")]
    public string? Fax { get; set; }

    [Required(ErrorMessage = "Enter an email address")]
    [StringLength(150, ErrorMessage = "Email (Primary) must not exceed 150 characters")]
    [OptionalEmailAddress(ErrorMessage = "Enter a valid email address")]
    public string? Email { get; set; }

    [StringLength(150, ErrorMessage = "Email (Secondary) must not exceed 150 characters")]
    [OptionalEmailAddress(ErrorMessage = "Enter a valid secondary email address")]
    public string? Email2 { get; set; }

    // Legacy captures Comments purely to include in the approval/decline notification email
    // (EmailUpdateNotifications). Email notification is not migrated - [NEEDS INVESTIGATION].
    [StringLength(2000, ErrorMessage = "Comments must not exceed 2000 characters")]
    public string? Comments { get; set; }
}

// Every ParticipantValidator (PTL.Core) rule is an unconditional primitive check (required/select/
// email format) with no cross-field, conditional, or domain logic, so DataAnnotations here fully
// mirror it - no IValidatableObject/Core delegation is needed. PTL.Core.Participant.ParticipantValidator
// remains the authoritative check enforced by the API (ParticipantService), unchanged.
public sealed class ParticipantFormViewModel
{
    public Guid? ParticipantId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SsoId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerQalNumber { get; set; } = string.Empty;

    [Display(Name = "Lab code")]
    [Required(ErrorMessage = "Enter a lab code")]
    public string? LabCode { get; set; } = string.Empty;

    [Display(Name = "Lab name")]
    [Required(ErrorMessage = "Enter a lab name")]
    public string? LabName { get; set; } = string.Empty;

    public Guid? LabTypeId { get; set; }

    // Populated by ParticipantController before the view is rendered - see /api/lookups/lab-types.
    public IEnumerable<SelectListItem> LabTypeOptions { get; set; } = [];

    [Display(Name = "Contact name")]
    [Required(ErrorMessage = "Enter a contact name")]
    public string? ContactName { get; set; } = string.Empty;

    [Display(Name = "Organisation name")]
    [Required(ErrorMessage = "Enter an organisation name")]
    public string? Organisation { get; set; } = string.Empty;

    [Display(Name = "Address line 1")]
    [Required(ErrorMessage = "Enter address line 1")]
    public string? Address1 { get; set; } = string.Empty;

    [Display(Name = "Address line 2")]
    [Required(ErrorMessage = "Enter address line 2")]
    public string? Address2 { get; set; } = string.Empty;

    [Display(Name = "Address line 3")]
    public string? Address3 { get; set; } = string.Empty;

    [Display(Name = "Address line 4")]
    public string? Address4 { get; set; } = string.Empty;

    [Display(Name = "Address line 5")]
    public string? Address5 { get; set; } = string.Empty;

    [Display(Name = "Country")]
    [NotEmptyGuid(ErrorMessage = "Select a country")]
    public Guid? CountryId { get; set; }

    // Populated by ParticipantController before the view is rendered - see /api/lookups/countries.
    public IEnumerable<SelectListItem> CountryOptions { get; set; } = [];

    [Display(Name = "Telephone")]
    [Required(ErrorMessage = "Enter a telephone number")]
    public string? Telephone { get; set; } = string.Empty;

    [Display(Name = "Fax")]
    public string? Fax { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [Required(ErrorMessage = "Enter an email address")]
    [OptionalEmailAddress(ErrorMessage = "Enter a valid email address")]
    public string? Email { get; set; } = string.Empty;

    [Display(Name = "Alternative email")]
    [OptionalEmailAddress(ErrorMessage = "Enter a valid alternative email address")]
    public string? Email2 { get; set; } = string.Empty;

    [Display(Name = "Other Packaging Requirements")]
    public string? Comments { get; set; } = string.Empty;

    // S6964 (value-type controller-action input) suppressed: this is an HTML checkbox, where an
    // unchecked box simply isn't posted and the framework's own asp-for-generated hidden companion
    // input already supplies "false" - non-nullable bool defaulting to false on under-posting is
    // the framework-intended behaviour, not a bug.
#pragma warning disable S6964
    public bool IsActive { get; set; } = true;
#pragma warning restore S6964

    // Display-only - server-generated by ParticipantService (set on deactivate, cleared on
    // reactivate/create), never posted back or trusted from the client. Only rendered on the Edit
    // form, and only when a value is present - never shown on Create, since the date is only ever
    // generated after the participant is first saved.
    public DateTime? InactiveDate { get; set; }

    // Populated by ParticipantController from the parent Customer (ICustomerApiClient) so
    // _ParticipantForm.cshtml's "Copy from customer contact" button - mirroring legacy
    // Participant.aspx.vb ButtonCopyDetails_Click - can copy client-side with no extra round trip.
    public string CustomerContactName { get; set; } = string.Empty;
    public string CustomerOrganisation { get; set; } = string.Empty;
    public string CustomerAddress1 { get; set; } = string.Empty;
    public string CustomerAddress2 { get; set; } = string.Empty;
    public string CustomerAddress3 { get; set; } = string.Empty;
    public string CustomerAddress4 { get; set; } = string.Empty;
    public string CustomerAddress5 { get; set; } = string.Empty;
    public Guid? CustomerCountryId { get; set; }
    public string CustomerTelephone { get; set; } = string.Empty;
    public string CustomerFax { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
}
