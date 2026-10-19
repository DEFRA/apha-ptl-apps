namespace PTL.Core.Search;

// Criteria as dbo.spgSearchCustomer takes them: '' means "do not filter", Guid.Empty means "any country".
public sealed record CustomerSearchCriteria(
    string QalNumber,
    string AccountNumber,
    string Name,
    string ContactName,
    string Organisation,
    Guid CountryId);

public sealed record ParticipantSearchCriteria(string LabCode, string LabName, string ContactName);

// Keyless projections of dbo.spgSearchCustomer / dbo.spgSearchParticipant result rows.
public class CustomerSearchRow
{
    public Guid CustomerId { get; set; }
    public string? QalNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string? Name { get; set; }
    public string? ContactName { get; set; }
    public string? Organisation { get; set; }
    public string? Country { get; set; }
}

public class ParticipantSearchRow
{
    public Guid ParticipantId { get; set; }
    public Guid CustomerId { get; set; }
    public string? LabCode { get; set; }
    public string? OldLabCode { get; set; }
    public string? LabName { get; set; }
    public string? ContactName { get; set; }
}
