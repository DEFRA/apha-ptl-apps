using PTL.Contracts.ExternalUser;

namespace PTL.ExternalWeb.Features.Lab;

public sealed class LabSelectionViewModel
{
    /// <summary>The lab chosen on the form.</summary>
    public Guid? SelectedLabId { get; set; }

    /// <summary>The labs to choose from; filled by the controller, never posted.</summary>
    public IReadOnlyList<ExternalLab> Labs { get; set; } = [];

    /// <summary>True when the user is already working on a lab and is switching, rather than choosing the first one.</summary>
    public bool IsSwitch { get; set; }
}
