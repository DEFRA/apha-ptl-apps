namespace PTL.Core.Lookup;

// Keyless domain projection for the list returned by spgaYearCurrent (current + next year only,
// matching legacy Contract.aspx.vb SetYearDropDown()). fldYear is a display string (e.g. "2025/26").
public class YearEntity
{
    public int YearId { get; set; }
    public string Year { get; set; } = string.Empty;
}
