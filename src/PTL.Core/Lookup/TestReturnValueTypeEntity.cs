namespace PTL.Core.Lookup;

// Keyless domain projection for the list returned by spgaTestReturnValueType (tblTestReturnValueType).
// Backs the Return Type dropdown on the SchemeAdmin "Test Result Items" screen - see
// docs/Parity-Analysis/11-InternalWeb-SchemeItemTypes-Parity-Analysis.md, Slice 4.
public class TestReturnValueTypeEntity
{
    public Guid TestReturnValueTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
}
