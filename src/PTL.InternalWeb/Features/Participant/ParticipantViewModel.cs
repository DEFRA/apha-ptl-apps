using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.Participant;

public sealed record ParticipantListViewModel(
    Guid CustomerId,
    string? SearchTerm,
    bool IncludeInactive,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<PTL.Contracts.Participant.ParticipantSummaryResponse> Participants);

public sealed class ParticipantFormViewModel
{
    public Guid ParticipantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid SsoId { get; set; }

    [Required]
    public string LabCode { get; set; } = string.Empty;

    [Required]
    public string LabName { get; set; } = string.Empty;

    public Guid LabTypeId { get; set; }

    [Required]
    public string ContactName { get; set; } = string.Empty;

    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public Guid CountryId { get; set; }
    public string Telephone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    [EmailAddress]
    public string Email2 { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class DeactivateParticipantViewModel
{
    public Guid ParticipantId { get; set; }
    public string LabName { get; set; } = string.Empty;
}
