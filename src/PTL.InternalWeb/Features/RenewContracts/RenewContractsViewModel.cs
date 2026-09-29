using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.RenewContracts;

// Legacy MergeContracts.aspx / MergeContractsViewModel - screen users know as "Renew Contracts".
public sealed class RenewContractsViewModel
{
    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerOrganisation { get; set; } = string.Empty;

    public string QalNumber { get; set; } = string.Empty;

    public bool IsAllowed { get; set; } = true;

    public string? BlockedReason { get; set; }

    public IReadOnlyList<RenewableContractDto> Contracts { get; set; } = [];

    public IReadOnlyList<RenewableContractItemDto> Items { get; set; } = [];

    public IReadOnlyList<string> ExistingSignatories { get; set; } = [];

    // Posted-back selections (Create/redisplay-on-error).
    public List<Guid> SelectedContractIds { get; set; } = [];

    public List<Guid> SelectedParticipantSchemeIds { get; set; } = [];

    public string? NewContractSignatory { get; set; }

    public string? ErrorMessage { get; set; }

    // Legacy MergeContractsViewModel.GetDisplayModel(): duplicate Identifiers collapse to one row,
    // ordered by lab code then old scheme.
    public IReadOnlyList<RenewContractsItemRow> ItemRows =>
        Items
            .GroupBy(i => i.Identifier, StringComparer.Ordinal)
            .Select(g => new RenewContractsItemRow(g.Key, g.First(), g.ToList()))
            .OrderBy(r => int.TryParse(r.First.LabCode, out var labCode) ? labCode : int.MaxValue)
            .ThenBy(r => r.First.OldSchemeIdentifier, StringComparer.Ordinal)
            .ToList();

    // Legacy ParticpiantSchemeViewModel.IsVisible - an item is only shown while its contract is selected.
    public bool IsRowVisible(RenewContractsItemRow row) =>
        row.Items.Any(i => SelectedContractIds.Contains(i.ContractId));

    public bool HasNoItems => !ItemRows.Any(IsRowVisible);
}

public sealed record RenewContractsItemRow(
    string Identifier,
    RenewableContractItemDto First,
    IReadOnlyList<RenewableContractItemDto> Items);
