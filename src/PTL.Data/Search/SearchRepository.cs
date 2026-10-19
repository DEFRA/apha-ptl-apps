using Dapper;
using PTL.Core.Search;
using PTL.Data.Infrastructure;

namespace PTL.Data.Search;

// Executes the existing legacy search stored procedures (Contracts Admin/Search.aspx) through
// Dapper; no database objects are created or changed.
public sealed class SearchRepository(IDbConnectionFactory connectionFactory) : ISearchRepository
{
    public async Task<IReadOnlyList<CustomerSearchRow>> SearchCustomersAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<CustomerSearchRow>(
            "EXEC dbo.spgSearchCustomer @QalNumber, @AccountNumber, @Name, @ContactName, @Organisation, @CountryId",
            new
            {
                criteria.QalNumber,
                criteria.AccountNumber,
                criteria.Name,
                criteria.ContactName,
                criteria.Organisation,
                criteria.CountryId,
            })).ToList();
    }

    public async Task<IReadOnlyList<ParticipantSearchRow>> SearchParticipantsAsync(ParticipantSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<ParticipantSearchRow>(
            "EXEC dbo.spgSearchParticipant @LabCode, @LabName, @ContactName",
            new { criteria.LabCode, criteria.LabName, criteria.ContactName })).ToList();
    }
}
