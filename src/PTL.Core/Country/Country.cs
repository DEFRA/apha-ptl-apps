namespace PTL.Core.Country;

// Keyless domain projection for the rows returned by spgaCountry, used by the Country Management
// admin screen - PTL.Core.Lookup.CountryEntity stays the thin projection for other domains'
// dropdowns. AllocationCount comes straight from dbo.fnCountCountryAllocationById, which already
// counts tblCustomer.fldCountryId, tblCustomer.fldInvoiceCountryId, tblParticipant.fldCountryId
// and tblGroupAddress.fldCountryId.
public sealed class Country
{
    public Guid CountryId { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public Guid CountryTypeId { get; set; }
    public string CountryType { get; set; } = string.Empty;
    public int AllocationCount { get; set; }
}
