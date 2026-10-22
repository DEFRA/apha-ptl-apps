namespace PTL.Core.Participant;

// What a Contract Item needs to know about its Scheme before it exists as a row - legacy
// ParticipantScheme.SchemeId's setter: CanEditX comes from Scheme.CanEditXParticipants (or the
// Overseas variant for a non-UK customer; dbo.fnIsDistributionNotPosted[Overseas]) and Warning from
// ParticipantSchemeWarning.GetWarning. Index 0 = January.
public sealed class ParticipantSchemeContext
{
    public IReadOnlyList<bool> CanEdit { get; init; } = Enumerable.Repeat(true, ParticipantSchemeMonths.Count).ToArray();

    // Months (1-12) of this scheme whose distribution is already initialised but not yet distributed.
    public IReadOnlyList<int> InitialisedMonthIds { get; init; } = [];
}

// Another contract item for the same participant and scheme (spgParticipantSchemeInfoByParticipantIdAndSchemeId).
public sealed class OtherParticipantScheme
{
    public Guid ParticipantSchemeId { get; init; }

    public bool IsRemoved { get; init; }

    public IReadOnlyList<bool> Months { get; init; } = new bool[ParticipantSchemeMonths.Count];
}
