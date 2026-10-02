using System.Data;
using PTL.Data.Contract.Renewal;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

public class ContractRenewalRepositoryTests
{
    public ContractRenewalRepositoryTests() => DapperColumnMappings.Register();

    private const string GetSql = @"
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

    private static DataTable RenewalTable(Guid contractId, Guid customerId)
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldQALNumber", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldAddress1", typeof(string));
        table.Columns.Add("fldAddress2", typeof(string));
        table.Columns.Add("fldAddress3", typeof(string));
        table.Columns.Add("fldAddress4", typeof(string));
        table.Columns.Add("fldAddress5", typeof(string));
        table.Columns.Add("fldCountry", typeof(string));
        table.Columns.Add("ContractStartDate", typeof(DateTime));
        table.Columns.Add("ContractEndDate", typeof(DateTime));
        table.Columns.Add("RenewalInformation", typeof(string));
        table.Rows.Add(contractId, customerId, "QAL/00001", "Sample Labs Ltd", "Alice Example",
            "1 Street", "Town", "County", "Country", "Postcode", "United Kingdom",
            new DateTime(2026, 4, 1), new DateTime(2027, 3, 31), "Renewal info");
        return table;
    }

    private static (ContractRenewalRepository Repository, PTL.Data.Tests.Fakes.FakeDbConnection Connection) CreateRepository()
    {
        var connection = new PTL.Data.Tests.Fakes.FakeDbConnection();
        var repository = new ContractRenewalRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByContractIdAsync_Found_ReturnsMatchingEntity()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToQuery(GetSql, RenewalTable(contractId, customerId));

        var result = await repository.GetByContractIdAsync(contractId);

        Assert.NotNull(result);
        Assert.Equal(contractId, result!.ContractId);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal("Renewal info", result.RenewalInformation);
    }

    [Fact]
    public async Task GetByContractIdAsync_MapsEveryLegacyField()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        connection.RespondToQuery(GetSql, RenewalTable(contractId, customerId));

        var result = await repository.GetByContractIdAsync(contractId);

        Assert.NotNull(result);
        Assert.Equal(contractId, result!.ContractId);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal("QAL/00001", result.QalNumber);
        Assert.Equal("Sample Labs Ltd", result.OrganisationName);
        Assert.Equal("Alice Example", result.ContactName);
        Assert.Equal("1 Street", result.Address1);
        Assert.Equal("Town", result.Address2);
        Assert.Equal("County", result.Address3);
        Assert.Equal("Country", result.Address4);
        Assert.Equal("Postcode", result.Address5);
        Assert.Equal("United Kingdom", result.Country);
        Assert.Equal(new DateTime(2026, 4, 1), result.ContractStartDate);
        Assert.Equal(new DateTime(2027, 3, 31), result.ContractEndDate);
        Assert.Equal("Renewal info", result.RenewalInformation);
    }

    [Fact]
    public async Task GetByContractIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSql, new DataTable());

        var result = await repository.GetByContractIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ContractRenewalService_DelegatesToRepository()
    {
        var expected = new PTL.Core.Contract.Renewal.ContractRenewalEntity
        {
            ContractId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            QalNumber = "QAL/00002",
            OrganisationName = "Delegated Labs",
            ContactName = "Bob Builder",
            Address1 = "2 Avenue",
            Address2 = "City",
            Address3 = "Region",
            Address4 = "Nation",
            Address5 = "ZIP 101",
            Country = "Scotland",
            ContractStartDate = new DateTime(2025, 1, 1),
            ContractEndDate = new DateTime(2026, 12, 31),
            RenewalInformation = "Delegated info"
        };

        var repository = new StubContractRenewalRepository(expected);
        var service = new PTL.Core.Contract.Renewal.ContractRenewalService(repository);

        var result = await service.GetByContractIdAsync(expected.ContractId);

        Assert.Same(expected, result);
        Assert.Equal(expected.ContractId, repository.ReceivedContractId);
    }

    private sealed class StubContractRenewalRepository(PTL.Core.Contract.Renewal.ContractRenewalEntity? entity) : PTL.Core.Contract.Renewal.IContractRenewalRepository
    {
        public Guid ReceivedContractId { get; private set; }

        public Task<PTL.Core.Contract.Renewal.ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default)
        {
            ReceivedContractId = contractId;
            return Task.FromResult(entity);
        }
    }
}
