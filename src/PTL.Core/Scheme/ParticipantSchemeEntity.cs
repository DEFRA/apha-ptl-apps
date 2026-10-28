namespace PTL.Core.Scheme;

// Lightweight projection for GET /api/schemes/participant/{participantId} - the existing legacy
// spgCommercialSchemesBySsoId/spgNonCommercialSchemesBySsoId procedures return exactly these columns
// (see ISchemeRepository remarks). Not the same shape as Scheme/SchemeSummaryEntity, which carry no
// participation concept at all.
public sealed class ParticipantSchemeEntity
{
    public Guid SchemeId { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    // fldYear as returned by the stored procedure (legacy SchemeList.Year is also a plain string).
    public string Year { get; set; } = string.Empty;

    public bool IsParticipating { get; set; }
}
