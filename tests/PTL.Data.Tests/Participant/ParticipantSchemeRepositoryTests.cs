using System.Data;
using PTL.Core.Participant;
using PTL.Data.Participant;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Participant;

// Exercises ParticipantSchemeRepository against a fake ADO.NET connection (see Fakes/) - proves
// the repository builds the right "EXEC dbo.spXxx" command text/parameters and correctly
// materialises the dynamic row into a ParticipantSchemeRecord, without needing a database.
public class ParticipantSchemeRepositoryTests
{
    private const string GetByIdSql = "EXEC dbo.spgParticipantSchemeByParticipantSchemeId @ParticipantSchemeId";

    private const string InsertSql =
        "EXEC dbo.spiParticipantScheme @ParticipantSchemeId, @ParticipantId, @SchemeId, @ContractId, @GroupAddressId, " +
        "@DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, " +
        "@DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, " +
        "@NumberOfSetsRequired, @ExternalReference, @Contact, @IsRemoved, @ImportExportLicenceRequired, @CustomsCertificateRequired, @NonFeePaying, " +
        "@PackingInstructions, @IsWeightedPricing, @DataConsentGiven, " +
        "@IsOverrideJan, @IsOverrideFeb, @IsOverrideMar, @IsOverrideApr, @IsOverrideMay, @IsOverrideJun, " +
        "@IsOverrideJul, @IsOverrideAug, @IsOverrideSep, @IsOverrideOct, @IsOverrideNov, @IsOverrideDec";

    private const string UpdateSql =
        "EXEC dbo.spuParticipantScheme @ParticipantSchemeId, @ParticipantId, @SchemeId, @ContractId, @GroupAddressId, " +
        "@DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, " +
        "@DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, " +
        "@NumberOfSetsRequired, @ExternalReference, @Contact, @IsRemoved, @ImportExportLicenceRequired, @CustomsCertificateRequired, @NonFeePaying, " +
        "@PackingInstructions, @IsWeightedPricing, @DataConsentGiven, " +
        "@IsOverrideJan, @IsOverrideFeb, @IsOverrideMar, @IsOverrideApr, @IsOverrideMay, @IsOverrideJun, " +
        "@IsOverrideJul, @IsOverrideAug, @IsOverrideSep, @IsOverrideOct, @IsOverrideNov, @IsOverrideDec";

    private static DataTable RowTable(Guid participantSchemeId, Guid contractId, Guid participantId, Guid schemeId, Guid? groupAddressId = null)
    {
        var table = new DataTable();
        void AddBool(string name) => table.Columns.Add(name, typeof(bool));
        table.Columns.Add("fldParticipantSchemeId", typeof(Guid));
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldGroupAddressId", typeof(Guid));
        foreach (var month in new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" })
        {
            AddBool($"fld{month}");
            AddBool($"fldCanEdit{month}");
            AddBool($"fldIsOverride{month}");
        }
        table.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        table.Columns.Add("fldExternalReference", typeof(string));
        table.Columns.Add("fldContact", typeof(string));
        AddBool("fldIsRemoved");
        AddBool("fldImportExportLicenceRequired");
        AddBool("fldCustomsCertificateRequired");
        AddBool("fldNonFeePaying");
        table.Columns.Add("fldPackingInstructions", typeof(string));
        AddBool("fldIsWeightedPricing");
        AddBool("fldDataConsentDeclarationGiven");
        table.Columns.Add("fldPrice", typeof(decimal));
        table.Columns.Add("fldParticipantName", typeof(string));
        table.Columns.Add("fldSchemeName", typeof(string));

        var row = table.NewRow();
        row["fldParticipantSchemeId"] = participantSchemeId;
        row["fldContractId"] = contractId;
        row["fldParticipantId"] = participantId;
        row["fldSchemeId"] = schemeId;
        row["fldGroupAddressId"] = (object?)groupAddressId ?? DBNull.Value;
        foreach (var month in new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" })
        {
            row[$"fld{month}"] = true;
            row[$"fldCanEdit{month}"] = true;
            row[$"fldIsOverride{month}"] = false;
        }
        row["fldNumberOfSetsRequired"] = 2;
        row["fldExternalReference"] = "EXT-001";
        row["fldContact"] = "Alice Example";
        row["fldIsRemoved"] = false;
        row["fldImportExportLicenceRequired"] = true;
        row["fldCustomsCertificateRequired"] = false;
        row["fldNonFeePaying"] = false;
        row["fldPackingInstructions"] = "Fragile";
        row["fldIsWeightedPricing"] = true;
        row["fldDataConsentDeclarationGiven"] = true;
        row["fldPrice"] = 42.5m;
        row["fldParticipantName"] = "Lab One: LAB1";
        row["fldSchemeName"] = "S1: Salmonella";
        table.Rows.Add(row);
        return table;
    }

    private static ParticipantSchemeRecord SampleRecord(Guid participantSchemeId, Guid contractId, Guid participantId, Guid schemeId) => new()
    {
        ParticipantSchemeId = participantSchemeId,
        ContractId = contractId,
        ParticipantId = participantId,
        SchemeId = schemeId,
        DistributionMonthJan = true,
        NumberOfSetsRequired = 2,
        ExternalReference = "EXT-001",
        Contact = "Alice Example",
        ImportExportLicenceRequired = true,
        PackingInstructions = "Fragile",
        IsWeightedPricing = true,
        DataConsentDeclarationGiven = true
    };

    private static (ParticipantSchemeRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new ParticipantSchemeRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedRecord()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var groupAddressId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, RowTable(participantSchemeId, contractId, participantId, schemeId, groupAddressId));

        var result = await repository.GetByIdAsync(participantSchemeId);

        Assert.NotNull(result);
        Assert.Equal(participantSchemeId, result!.ParticipantSchemeId);
        Assert.Equal(contractId, result.ContractId);
        Assert.Equal(participantId, result.ParticipantId);
        Assert.Equal(schemeId, result.SchemeId);
        Assert.Equal(groupAddressId, result.GroupAddressId);
        Assert.True(result.DistributionMonthJan);
        Assert.True(result.CanEditJan);
        Assert.False(result.IsOverrideJan);
        Assert.Equal(2, result.NumberOfSetsRequired);
        Assert.Equal("EXT-001", result.ExternalReference);
        Assert.Equal("Alice Example", result.Contact);
        Assert.False(result.IsRemoved);
        Assert.True(result.ImportExportLicenceRequired);
        Assert.False(result.CustomsCertificateRequired);
        Assert.False(result.NonFeePaying);
        Assert.Equal("Fragile", result.PackingInstructions);
        Assert.True(result.IsWeightedPricing);
        Assert.True(result.DataConsentDeclarationGiven);
        Assert.Equal(42.5m, result.Price);
        Assert.Equal("Lab One: LAB1", result.ParticipantDisplayName);
        Assert.Equal("S1: Salmonella", result.SchemeDisplayName);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var emptyTable = RowTable(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        emptyTable.Rows.Clear();
        connection.RespondToQuery(GetByIdSql, emptyTable);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_NullGroupAddressId_MapsToNull()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, RowTable(participantSchemeId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        var result = await repository.GetByIdAsync(participantSchemeId);

        Assert.NotNull(result);
        Assert.Null(result!.GroupAddressId);
    }

    [Fact]
    public async Task CreateAsync_InsertsAndReReadsRecord()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, RowTable(participantSchemeId, contractId, participantId, schemeId));

        var result = await repository.CreateAsync(SampleRecord(participantSchemeId, contractId, participantId, schemeId));

        Assert.Equal(participantSchemeId, result.ParticipantSchemeId);
        Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
    }

    [Fact]
    public async Task CreateAsync_InsertedButNotReReadable_Throws()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        var emptyTable = RowTable(participantSchemeId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        emptyTable.Rows.Clear();
        connection.RespondToQuery(GetByIdSql, emptyTable);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.CreateAsync(SampleRecord(participantSchemeId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public async Task UpdateAsync_RowsAffected_ReturnsTrue()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 1);

        var result = await repository.UpdateAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(result);
    }

    [Fact]
    public async Task UpdateAsync_NoRowsAffected_ReturnsFalse()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 0);

        var result = await repository.UpdateAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(result);
    }
}
