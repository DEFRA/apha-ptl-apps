namespace PTL.Core.Scheme;

// Keyless domain projection for the list returned by spgSchemeInfoBySharedId (scheme family
// history, ordered newest year first by the stored procedure's own ORDER BY).
public class SchemeHistoryEntity
{
    public Guid SchemeId { get; set; }
    public Guid SharedId { get; set; }
    public int YearId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
