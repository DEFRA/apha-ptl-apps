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
        ConfigureNoDistributions(connection);
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
    public async Task GetByIdAsync_MissingOptionalBoolColumn_DefaultsToFalse()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        var table = RowTable(participantSchemeId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        table.Columns.Remove("fldIsWeightedPricing");
        connection.RespondToQuery(GetByIdSql, table);

        var result = await repository.GetByIdAsync(participantSchemeId);

        Assert.NotNull(result);
        Assert.False(result!.IsWeightedPricing);
    }

    [Fact]
    public async Task GetByIdAsync_NullBoolColumn_DefaultsToFalse()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        var table = RowTable(participantSchemeId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        table.Rows[0]["fldIsRemoved"] = DBNull.Value;
        connection.RespondToQuery(GetByIdSql, table);

        var result = await repository.GetByIdAsync(participantSchemeId);

        Assert.NotNull(result);
        Assert.False(result!.IsRemoved);
    }

    [Fact]
    public async Task GetByIdAsync_NullPriceAndNumberOfSetsRequired_DefaultToZeroAndFallback()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        var table = RowTable(participantSchemeId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        table.Rows[0]["fldPrice"] = DBNull.Value;
        table.Rows[0]["fldNumberOfSetsRequired"] = DBNull.Value;
        connection.RespondToQuery(GetByIdSql, table);

        var result = await repository.GetByIdAsync(participantSchemeId);

        Assert.NotNull(result);
        Assert.Equal(0m, result!.Price);
        Assert.Equal(1, result.NumberOfSetsRequired);
    }

    [Fact]
    public async Task CreateAsync_NullOptionalTextFields_CoalescesToEmptyString()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, RowTable(participantSchemeId, contractId, participantId, schemeId));
        var record = SampleRecord(participantSchemeId, contractId, participantId, schemeId);
        record.ExternalReference = null;
        record.Contact = null;
        record.PackingInstructions = null;

        await repository.CreateAsync(record);

        var insertCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(string.Empty, insertCommand.ParameterValue("@ExternalReference"));
        Assert.Equal(string.Empty, insertCommand.ParameterValue("@Contact"));
        Assert.Equal(string.Empty, insertCommand.ParameterValue("@PackingInstructions"));
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
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 1);

        var result = await repository.UpdateAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(result);
    }

    [Fact]
    public async Task UpdateAsync_NoRowsAffected_ReturnsFalse()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 0);

        var result = await repository.UpdateAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(result);
    }

    private const string YearSql = "SELECT fldYearId FROM tblContract WHERE fldContractId = @ContractId";
    private const string SettingsSql = "EXEC dbo.spgaSystemSettings";
    private const string MonthSql = "EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId";
    private const string AlreadyOnSql = "SELECT COUNT(*) FROM tlnkDistributionParticipant WHERE fldParticipantId = @ParticipantId AND fldDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string MismatchSql = "EXEC dbo.spgCheckForMissmatchContract @ParticipantId, @ContractId";
    private const string AddParticipantSql =
        "EXEC dbo.spiDistributionParticipant @DistributionParticipantId = @DistributionParticipantId, @ParticipantId = @ParticipantId, " +
        "@MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId, @DeadlineDate = NULL, @DeadlineChangeReason = '', @AdditionalParticipant = 0";
    private const string RemoveParticipantSql = "EXEC dbo.spdDistributionParticipant @DistributionParticipantId";
    private const string SampleReceiptSql = "EXEC dbo.spiSetupSampleReceipt @MonthlyDistributionSchemeId";

    private const string AuditSql = "EXEC dbo.spiAuditParticipantScheme @ParticipantSchemeId, @ContractId, @ParticipantId, @SchemeId, @AuditWho, @AuditWhy";

    // Contract year 2026 starting in April, and no month initialised - the quiet default for every test.
    private static void ConfigureNoDistributions(FakeDbConnection connection)
    {
        var year = new DataTable();
        year.Columns.Add("fldYearId", typeof(int));
        year.Rows.Add(2026);
        connection.RespondToQuery(YearSql, year);

        var settings = new DataTable();
        settings.Columns.Add("fldContractStartDate", typeof(DateTime));
        settings.Rows.Add(new DateTime(2000, 4, 1));
        connection.RespondToQuery(SettingsSql, settings);

        connection.RespondToQuery(MonthSql, MonthDataSet(null));
        connection.RespondToScalar(AlreadyOnSql, 0);
        connection.RespondToQuery(MismatchSql, new DataTable());
        connection.RespondToNonQuery(AddParticipantSql, 1);
        connection.RespondToNonQuery(RemoveParticipantSql, 1);
        connection.RespondToNonQuery(SampleReceiptSql, 1);
    }

    // One month's spgMonthlyDistribution result: header, schemes, participants. A null scheme means no distribution.
    private static DataSet MonthDataSet(Guid? schemeId, Guid? existingParticipantId = null, Guid? existingContractId = null, string suffix = "", Guid? mdsId = null)
    {
        var header = new DataTable();
        header.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        var schemes = new DataTable();
        schemes.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        schemes.Columns.Add("fldSchemeId", typeof(Guid));
        schemes.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        var participants = new DataTable();
        participants.Columns.Add("fldDistributionParticipantId", typeof(Guid));
        participants.Columns.Add("fldParticipantId", typeof(Guid));
        participants.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        participants.Columns.Add("fldContractId", typeof(Guid));

        if (schemeId is not null)
        {
            var scheme = mdsId ?? Guid.NewGuid();
            header.Rows.Add(Guid.NewGuid());
            schemes.Rows.Add(scheme, schemeId, suffix);
            if (existingParticipantId is not null)
            {
                participants.Rows.Add(Guid.NewGuid(), existingParticipantId, scheme, existingContractId ?? Guid.NewGuid());
            }
        }

        var set = new DataSet();
        set.Tables.Add(header);
        set.Tables.Add(schemes);
        set.Tables.Add(participants);
        return set;
    }

    private static List<string> Sent(FakeDbConnection connection) => [.. connection.ExecutedCommands.Select(c => c.CommandText)];

    [Fact]
    public async Task CreateAsync_NoMonthInitialised_OnlyInsertsTheItemInsideACommittedTransaction()
    {
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, RowTable(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        connection.RespondToNonQuery(InsertSql, 1);

        await repository.CreateAsync(SampleRecord(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.DoesNotContain(AddParticipantSql, Sent(connection));
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task CreateAsync_SelectedMonthWithDistribution_AddsTheParticipantAfterTheItemAndSetsUpSampleReceipts()
    {
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        var record = SampleRecord(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        connection.RespondToQuery(GetByIdSql, RowTable(id, record.ContractId, record.ParticipantId, record.SchemeId));
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(MonthSql, MonthDataSet(record.SchemeId));

        await repository.CreateAsync(record);

        var sent = Sent(connection);
        Assert.Single(sent, AddParticipantSql);
        Assert.Single(sent, SampleReceiptSql);
        Assert.True(sent.IndexOf(InsertSql) < sent.IndexOf(AddParticipantSql), "legacy Insert saves the item first");
        Assert.True(sent.IndexOf(AddParticipantSql) < sent.IndexOf(SampleReceiptSql));
        Assert.Equal(record.ParticipantId, connection.ExecutedCommands.Single(c => c.CommandText == AddParticipantSql).ParameterValue("@ParticipantId"));
    }

    [Fact]
    public async Task CreateAsync_ParticipantAlreadyOnTheDistribution_IsNotAddedTwice()
    {
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        var record = SampleRecord(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        connection.RespondToQuery(GetByIdSql, RowTable(id, record.ContractId, record.ParticipantId, record.SchemeId));
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(MonthSql, MonthDataSet(record.SchemeId));
        connection.RespondToScalar(AlreadyOnSql, 1);

        await repository.CreateAsync(record);

        Assert.DoesNotContain(AddParticipantSql, Sent(connection));
        Assert.DoesNotContain(SampleReceiptSql, Sent(connection));
    }

    [Fact]
    public async Task CreateAsync_ParticipantOfAnotherCustomer_ThrowsAndDoesNotCommit()
    {
        var (repository, connection) = CreateRepository();
        var record = SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(MonthSql, MonthDataSet(record.SchemeId));
        var mismatch = new DataTable();
        mismatch.Columns.Add("fldParticipantSchemeId", typeof(Guid));
        mismatch.Rows.Add(Guid.NewGuid());
        connection.RespondToQuery(MismatchSql, mismatch);

        var ex = await Assert.ThrowsAsync<ParticipantSchemeValidationException>(() => repository.CreateAsync(record));

        Assert.Contains("mismatch of customers", ex.Errors[0].Message);
        Assert.False(connection.LastTransaction!.Committed);
        Assert.DoesNotContain(AddParticipantSql, Sent(connection));
    }

    [Fact]
    public async Task UpdateAsync_MonthNoLongerSelected_RemovesTheParticipantBeforeTheItemIsUpdated()
    {
        var (repository, connection) = CreateRepository();
        var record = SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 1);
        connection.RespondToQuery(MonthSql, MonthDataSet(record.SchemeId, record.ParticipantId, record.ContractId));

        await repository.UpdateAsync(record);

        var sent = Sent(connection);
        // Only January is ticked, so the other eleven months' rows go; the participant stays on January.
        Assert.Equal(11, sent.Count(s => s == RemoveParticipantSql));
        Assert.DoesNotContain(AddParticipantSql, sent);
        Assert.True(sent.IndexOf(RemoveParticipantSql) < sent.IndexOf(UpdateSql + "; SELECT @@ROWCOUNT"), "legacy Update syncs before it saves");
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task UpdateAsync_ParticipantOnTheDistributionFromAnotherContract_IsNotRemoved()
    {
        var (repository, connection) = CreateRepository();
        var record = SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 1);
        connection.RespondToQuery(MonthSql, MonthDataSet(record.SchemeId, record.ParticipantId, Guid.NewGuid()));

        await repository.UpdateAsync(record);

        Assert.DoesNotContain(RemoveParticipantSql, Sent(connection));
    }

    [Fact]
    public async Task UpdateAsync_RepeatDistributionsAreIgnored()
    {
        var (repository, connection) = CreateRepository();
        var record = SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 1);
        connection.RespondToQuery(MonthSql, MonthDataSet(record.SchemeId, record.ParticipantId, record.ContractId, suffix: "a"));

        await repository.UpdateAsync(record);

        Assert.DoesNotContain(RemoveParticipantSql, Sent(connection));
        Assert.DoesNotContain(AddParticipantSql, Sent(connection));
    }

    [Fact]
    public async Task UpdateAsync_NoRowUpdated_RollsTheWholeSaveBack()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 0);

        await repository.UpdateAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task RemoveAsync_WritesTheAuditRowAfterTheSyncAndBeforeTheUpdateInOneCommittedTransaction()
    {
        var (repository, connection) = CreateRepository();
        var record = SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        record.IsRemoved = true;
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 1);
        connection.RespondToNonQuery(AuditSql, 1);
        connection.RespondToQuery(MonthSql, MonthDataSet(record.SchemeId, record.ParticipantId, record.ContractId));

        var result = await repository.RemoveAsync(record, new ParticipantSchemeAudit("jsmith", "Customer left"));

        Assert.True(result);
        var sent = Sent(connection);
        Assert.True(sent.IndexOf(RemoveParticipantSql) < sent.IndexOf(AuditSql), "the sync deletes the distribution rows first");
        Assert.True(sent.IndexOf(AuditSql) < sent.IndexOf(UpdateSql + "; SELECT @@ROWCOUNT"), "SetAudit runs before the save");
        var audit = connection.ExecutedCommands.Single(c => c.CommandText == AuditSql);
        Assert.Equal(record.ParticipantSchemeId, audit.ParameterValue("@ParticipantSchemeId"));
        Assert.Equal(record.ContractId, audit.ParameterValue("@ContractId"));
        Assert.Equal(record.ParticipantId, audit.ParameterValue("@ParticipantId"));
        Assert.Equal(record.SchemeId, audit.ParameterValue("@SchemeId"));
        Assert.Equal("jsmith", audit.ParameterValue("@AuditWho"));
        Assert.Equal("Customer left", audit.ParameterValue("@AuditWhy"));
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task RemoveAsync_NoRowUpdated_RollsTheAuditBackToo()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 0);
        connection.RespondToNonQuery(AuditSql, 1);

        var result = await repository.RemoveAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), new ParticipantSchemeAudit("jsmith", "why"));

        Assert.False(result);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task UpdateAsync_WithoutAudit_DoesNotWriteAnAuditRow()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 1);

        await repository.UpdateAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.DoesNotContain(AuditSql, Sent(connection));
    }

    [Fact]
    public async Task Sync_MonthsBeforeTheContractStartMonth_ReadTheFollowingYearsDistribution()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 1);

        await repository.UpdateAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        int YearFor(int month) => Convert.ToInt32(connection.ExecutedCommands.Single(c => c.CommandText == MonthSql && Convert.ToInt32(c.ParameterValue("@MonthId"), System.Globalization.CultureInfo.InvariantCulture) == month).ParameterValue("@YearId"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2027, YearFor(1));
        Assert.Equal(2027, YearFor(3));
        Assert.Equal(2026, YearFor(4));
        Assert.Equal(2026, YearFor(12));
    }

    [Fact]
    public async Task Sync_UnknownContract_SkipsTheDistributions()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(UpdateSql + "; SELECT @@ROWCOUNT", 1);
        connection.RespondToQuery(YearSql, new DataTable { Columns = { { "fldYearId", typeof(int) } } });

        await repository.UpdateAsync(SampleRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.DoesNotContain(MonthSql, Sent(connection));
    }

    private const string SchemeSql = "EXEC dbo.spgSchemeBySchemeId @SchemeId";
    private const string WarningSql = "EXEC dbo.spgParticipantSchemeWarning @SchemeId, @ContractId";
    private const string OthersSql = "EXEC dbo.spgParticipantSchemeInfoByParticipantIdAndSchemeId @ParticipantId, @SchemeId";

    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    // First result set of spgSchemeBySchemeId, trimmed to the columns the repository reads: UK flag for
    // February is false, Overseas flag for March is false, every other flag true.
    private static DataSet SchemeDataSet(bool includeRow = true)
    {
        var table = new DataTable();
        foreach (var month in Months)
        {
            table.Columns.Add($"fldCanEdit{month}Participants", typeof(bool));
            table.Columns.Add($"fldCanEdit{month}ParticipantsOverseas", typeof(bool));
        }

        if (includeRow)
        {
            var row = table.NewRow();
            foreach (var month in Months)
            {
                row[$"fldCanEdit{month}Participants"] = month != "Feb";
                row[$"fldCanEdit{month}ParticipantsOverseas"] = month != "Mar";
            }

            table.Rows.Add(row);
        }

        var dataSet = new DataSet();
        dataSet.Tables.Add(table);
        return dataSet;
    }

    private static DataTable WarningTable(params int[] monthIds)
    {
        var table = new DataTable();
        table.Columns.Add("fldMonthId", typeof(int));
        foreach (var monthId in monthIds)
        {
            table.Rows.Add(monthId);
        }

        return table;
    }

    [Fact]
    public async Task GetSchemeContextAsync_UkCustomer_UsesTheUkPostingFlagsAndReturnsInitialisedMonths()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(SchemeSql, SchemeDataSet());
        connection.RespondToQuery(WarningSql, WarningTable(4, 5, 4));

        var context = await repository.GetSchemeContextAsync(Guid.NewGuid(), Guid.NewGuid(), nonUk: false);

        Assert.False(context.CanEdit[1]);
        Assert.Equal(11, context.CanEdit.Count(canEdit => canEdit));
        Assert.Equal([4, 5], context.InitialisedMonthIds);
    }

    [Fact]
    public async Task GetSchemeContextAsync_NonUkCustomer_UsesTheOverseasPostingFlags()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(SchemeSql, SchemeDataSet());
        connection.RespondToQuery(WarningSql, WarningTable());

        var context = await repository.GetSchemeContextAsync(Guid.NewGuid(), Guid.NewGuid(), nonUk: true);

        Assert.False(context.CanEdit[2]);
        Assert.True(context.CanEdit[1]);
        Assert.Empty(context.InitialisedMonthIds);
    }

    [Fact]
    public async Task GetSchemeContextAsync_UnknownScheme_LeavesEveryMonthEditable()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(SchemeSql, SchemeDataSet(includeRow: false));
        connection.RespondToQuery(WarningSql, WarningTable());

        var context = await repository.GetSchemeContextAsync(Guid.NewGuid(), Guid.NewGuid(), nonUk: false);

        Assert.All(context.CanEdit, Assert.True);
    }

    [Fact]
    public async Task GetByParticipantAndSchemeAsync_MapsEachOtherItemsMonthsAndRemovedFlag()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldParticipantSchemeId", typeof(Guid));
        table.Columns.Add("fldIsRemoved", typeof(bool));
        foreach (var month in Months)
        {
            table.Columns.Add($"fld{month}", typeof(bool));
        }

        var id = Guid.NewGuid();
        var row = table.NewRow();
        row["fldParticipantSchemeId"] = id;
        row["fldIsRemoved"] = true;
        foreach (var month in Months)
        {
            row[$"fld{month}"] = month == "Mar";
        }

        table.Rows.Add(row);
        connection.RespondToQuery(OthersSql, table);

        var others = await repository.GetByParticipantAndSchemeAsync(Guid.NewGuid(), Guid.NewGuid());

        var other = Assert.Single(others);
        Assert.Equal(id, other.ParticipantSchemeId);
        Assert.True(other.IsRemoved);
        Assert.True(other.Months[2]);
        Assert.Equal(1, other.Months.Count(month => month));
    }
}
