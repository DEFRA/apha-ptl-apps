namespace PTL.Core.Lookup;

// Keyless domain projection for the list returned by spgaLabType.
public class LabTypeEntity
{
    public Guid LabTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
}
