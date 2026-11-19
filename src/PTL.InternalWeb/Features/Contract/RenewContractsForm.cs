namespace PTL.InternalWeb.Features.Contract;

// The Renew Contracts form as posted; property names match the page's field names.
public sealed class RenewContractsForm
{
    public List<Guid>? SelectedContractIds { get; set; }

    public List<Guid>? SelectedParticipantSchemeIds { get; set; }

    public string? NewContractSignatory { get; set; }

    public List<string>? SelectedItemIdentifiers { get; set; }

    public string? Command { get; set; }

    public bool SelectAllContracts { get; set; }

    public bool SelectAllItems { get; set; }

    public bool SelectionChanged { get; set; }
}
