namespace PTL.Core.Scheme;

// The Results Tabulations tab. Like the Tests tree this is staged in the form and written as a
// unit on Save. Availability is stored as two independent flags in tblTabulation but legacy
// presents them as a single three-way dropdown.
public sealed class SchemeTabulation
{
    public Guid TabulationId { get; set; }
    public Guid SchemeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IntendedResultsOnly { get; set; }
    public bool SingleParticipantTabulation { get; set; }
    public bool ShowRatings { get; set; }
    public bool AvailableToParticipants { get; set; }
    public bool AvailableToViewers { get; set; }

    // The result and method items this tabulation includes, by item id.
    public IList<Guid> ResultItemIds { get; set; } = [];
    public IList<Guid> MethodItemIds { get; set; } = [];

    // Populated on read only - the link rows behind the id lists, so a removed item can be
    // deleted by its own link id.
    public IList<SchemeTabulationItemLink> ResultItemLinks { get; set; } = [];
    public IList<SchemeTabulationItemLink> MethodItemLinks { get; set; } = [];
}

// tblTabulationTestResultItem / tblTabulationTestMethodItem. The link carries its own id so it
// can be deleted individually - there is no update procedure for either table.
public sealed class SchemeTabulationItemLink
{
    public Guid LinkId { get; set; }
    public Guid TabulationId { get; set; }
    public Guid ItemId { get; set; }
}
