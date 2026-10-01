using Dapper;
using PTL.Core.Contract.Renewal;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.Renewal;

/// <summary>
/// Legacy <c>MailMergeRenewalLetter.ExecuteSingleRenewalLetter</c> fetches the whole renewal
/// collection via the unparameterised <c>spgaExportContractRenewal</c> and then selects by contract
/// id in memory. That behaviour is reproduced exactly; the procedure has no by-id variant.
/// </summary>
public sealed class ContractRenewalRepository(IDbConnectionFactory connectionFactory) : IContractRenewalRepository
{
    private const string GetByContractIdSql = @"
DECLARE @YearId int = dbo.fnGetCurrentYearId();
DECLARE @ContractStartDate datetime;
SELECT @ContractStartDate = fldContractStartDate
FROM tblSystemSettings;
SET @ContractStartDate = CAST(DAY(@ContractStartDate) as varchar) + '/' + CAST(MONTH(@ContractStartDate) as varchar) + '/' + CAST(@YearId as varchar);

DECLARE @ContractEndDate datetime;
SET @ContractEndDate = DATEADD(year, 1, @ContractStartDate);
SET @ContractEndDate = DATEADD(day, -1, @ContractEndDate);

SELECT
    tblCustomer.fldCustomerId AS fldCustomerId,
    tblCustomer.fldQALNumber AS fldQALNumber,
    tblCustomer.fldName AS fldName,
    tblCustomer.fldOrganisation AS fldOrganisation,
    tblCustomer.fldContactName AS fldContactName,
    tblCustomer.fldAddress1 AS fldAddress1,
    tblCustomer.fldAddress2 AS fldAddress2,
    tblCustomer.fldAddress3 AS fldAddress3,
    tblCustomer.fldAddress4 AS fldAddress4,
    tblCustomer.fldAddress5 AS fldAddress5,
    tblCountry.fldCountry AS fldCountry,
    (dbo.fnConcatRenewalInformation(tblCustomer.fldCustomerId, @YearId)) AS RenewalInformation,
    @ContractStartDate AS ContractStartDate,
    @ContractEndDate AS ContractEndDate,
    tblContract.fldContractId AS fldContractId
FROM tblCustomer
INNER JOIN tblCountry ON tblCustomer.fldCountryId = tblCountry.fldCountryId
INNER JOIN tblContract ON tblCustomer.fldCustomerId = tblContract.fldCustomerId
WHERE tblContract.fldContractId = @ContractId;";

    public async Task<ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<ContractRenewalEntity>(
            GetByContractIdSql,
            new { ContractId = contractId });
    }
}
