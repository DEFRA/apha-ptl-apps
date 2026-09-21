using System.Data;
using PTL.Core.Scheme;
using PTL.Data.Infrastructure;
using PTL.Data.Scheme;
using PTL.Data.Tests.Fakes;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data.Tests.Scheme;

// Exercises SchemeRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL
// Server - see ContractRepositoryTests for the pattern this follows.
public class SchemeRepositoryTests
{
    public SchemeRepositoryTests() => DapperColumnMappings.Register();

    private const string GetByIdSql = "EXEC dbo.spgSchemeBySchemeId @SchemeId";
    private const string GetSummariesSql = "EXEC dbo.spgSchemeInfoByYearId @YearId";
    private const string GetHistorySql = "EXEC dbo.spgSchemeInfoBySharedId @SharedId";
    private const string InsertSql =
        "EXEC dbo.spiScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";
    private const string UpdateSql =
        "EXEC dbo.spuScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";

    private static DataTable SchemeTable(Guid schemeId, int yearId = 2026)
    {
        var table = new DataTable();
        table.Columns.Add("Readonly", typeof(bool));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldName", typeof(string));
        table.Rows.Add(false, schemeId, yearId, "Test Scheme");
        return table;
    }

    private static (SchemeRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new SchemeRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedScheme()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));

        var result = await repository.GetByIdAsync(schemeId);

        Assert.NotNull(result);
        Assert.Equal(schemeId, result!.SchemeId);
        Assert.Equal("Test Scheme", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByIdSql, new DataTable());

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSummariesByYearAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldCurrentSchemeId", typeof(Guid));
        table.Columns.Add("fldCurrentIdentifier", typeof(string));
        table.Columns.Add("fldCurrentName", typeof(string));
        table.Rows.Add(Guid.NewGuid(), 2026, Guid.NewGuid(), "PT0001", "Test Scheme");
        connection.RespondToQuery(GetSummariesSql, table);

        var result = await repository.GetSummariesByYearAsync(2026);

        Assert.Single(result);
        Assert.Equal(2026, result[0].YearId);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsMappedHistory()
    {
        var (repository, connection) = CreateRepository();
        var sharedId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCurrentSchemeId", typeof(Guid));
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldCurrentIdentifier", typeof(string));
        table.Columns.Add("fldCurrentName", typeof(string));
        table.Rows.Add(Guid.NewGuid(), sharedId, 2026, "PT0001", "Test Scheme");
        connection.RespondToQuery(GetHistorySql, table);

        var result = await repository.GetHistoryAsync(sharedId);

        Assert.Single(result);
        Assert.Equal(sharedId, result[0].SharedId);
    }

    [Fact]
    public async Task CreateAsync_InsertsAndReReadsScheme()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));
        var scheme = new CoreScheme { SchemeId = schemeId, YearId = 2026, Name = "Test Scheme" };

        var result = await repository.CreateAsync(scheme);

        Assert.Equal(schemeId, result.SchemeId);
        var insertCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(schemeId, insertCommand.ParameterValue("@SchemeId"));
    }

    [Fact]
    public async Task UpdateAsync_ExistingScheme_UpdatesAndReReads()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));
        var scheme = new CoreScheme { SchemeId = schemeId, YearId = 2026, Name = "Test Scheme" };

        var result = await repository.UpdateAsync(scheme);

        Assert.NotNull(result);
        Assert.Equal(schemeId, result!.SchemeId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownScheme_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 0);
        var scheme = new CoreScheme { SchemeId = Guid.NewGuid(), YearId = 2026, Name = "Test Scheme" };

        var result = await repository.UpdateAsync(scheme);

        Assert.Null(result);
    }
}
