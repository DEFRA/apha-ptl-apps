using System.Data;
using PTL.Contracts.Lookup;
using PTL.Data.Infrastructure;
using PTL.Data.Lookup;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Lookup;

// Exercises LookupRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL
// Server - see ContractRepositoryTests for the pattern this follows. Every method here is a
// parameterless (or single-parameter) read with no Create/Update counterpart.
public class LookupRepositoryTests
{
    public LookupRepositoryTests() => DapperColumnMappings.Register();

    private static (LookupRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new LookupRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetCountriesAsync_ReturnsMappedCountries()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldCountryId", typeof(Guid));
        table.Columns.Add("fldCountry", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "United Kingdom");
        connection.RespondToQuery("EXEC dbo.spgaCountry", table);

        var result = await repository.GetCountriesAsync();

        Assert.Single(result);
        Assert.Equal("United Kingdom", result[0].Country);
    }

    [Fact]
    public async Task GetCurrenciesAsync_ReturnsMappedCurrencies()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldCurrencyId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldSymbol", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "British Pound", "£");
        connection.RespondToQuery("EXEC dbo.spgaCurrency", table);

        var result = await repository.GetCurrenciesAsync();

        Assert.Single(result);
        Assert.Equal("£", result[0].Symbol);
    }

    [Fact]
    public async Task GetCustomerTypesAsync_ReturnsMappedCustomerTypes()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldCustomerTypeId", typeof(Guid));
        table.Columns.Add("fldCustomerType", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Commercial");
        connection.RespondToQuery("EXEC dbo.spgaCustomerType", table);

        var result = await repository.GetCustomerTypesAsync();

        Assert.Single(result);
        Assert.Equal("Commercial", result[0].CustomerType);
    }

    [Fact]
    public async Task GetVatRatingsAsync_ReturnsMappedVatRatings()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldVatRatingId", typeof(Guid));
        table.Columns.Add("fldVatRating", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Standard");
        connection.RespondToQuery("EXEC dbo.spgaVatRating", table);

        var result = await repository.GetVatRatingsAsync();

        Assert.Single(result);
        Assert.Equal("Standard", result[0].VatRating);
    }

    [Fact]
    public async Task GetLabTypesAsync_ReturnsMappedLabTypes()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldLabTypeId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Reference laboratory");
        connection.RespondToQuery("EXEC dbo.spgaLabType", table);

        var result = await repository.GetLabTypesAsync();

        Assert.Single(result);
        Assert.Equal("Reference laboratory", result[0].Name);
    }

    [Fact]
    public async Task GetCurrentYearsAsync_ReturnsMappedYears()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldYear", typeof(string));
        table.Rows.Add(2026, "2026/27");
        connection.RespondToQuery("EXEC dbo.spgaYearCurrent", table);

        var result = await repository.GetCurrentYearsAsync();

        Assert.Single(result);
        Assert.Equal(2026, result[0].YearId);
    }

    [Fact]
    public async Task GetSchemeCurrenciesAsync_ReturnsMappedSchemeCurrencies()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldSchemeCurrencyId", typeof(Guid));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldCurrencyId", typeof(Guid));
        table.Columns.Add("fldPrice", typeof(decimal));
        table.Columns.Add("fldCurrencyName", typeof(string));
        table.Columns.Add("fldCurrencySymbol", typeof(string));
        table.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 12.5m, "British Pound", "£");
        connection.RespondToQuery("EXEC dbo.spgaSchemeCurrency", table);

        var result = await repository.GetSchemeCurrenciesAsync();

        Assert.Single(result);
        Assert.Equal(12.5m, result[0].Price);
    }

    [Fact]
    public async Task GetPostagePricingPlansForYearAsync_ReturnsMappedPlans()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldPostageId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldUkPrice", typeof(decimal));
        table.Columns.Add("fldYearId", typeof(int));
        table.Rows.Add(Guid.NewGuid(), "Standard", 5.5m, 2026);
        connection.RespondToQuery("EXEC dbo.spgPostageByYearID @YearId", table);

        var result = await repository.GetPostagePricingPlansForYearAsync(2026);

        Assert.Single(result);
        Assert.Equal("Standard", result[0].Name);
    }

    [Fact]
    public async Task GetAllYearsAsync_ReturnsMappedYears()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldYear", typeof(string));
        table.Rows.Add(2025, "2025/26");
        table.Rows.Add(2026, "2026/27");
        connection.RespondToQuery("EXEC dbo.spgaYear", table);

        var result = await repository.GetAllYearsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("2025/26", result[0].Year);
    }

    [Fact]
    public async Task GetWeightedPricingYearsAsync_ReturnsMappedYears()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldYear", typeof(string));
        table.Rows.Add(2026, "2026/27");
        connection.RespondToQuery("EXEC dbo.spgaWeightedPricingYear", table);

        var result = await repository.GetWeightedPricingYearsAsync();

        Assert.Single(result);
        Assert.Equal("2026/27", result[0].Year);
    }

    [Fact]
    public async Task GetGroupAddressesAsync_ReturnsMappedGroupAddresses()
    {
        var (repository, connection) = CreateRepository();
        var countryId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldGroupAddressId", typeof(Guid));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldAddress1", typeof(string));
        table.Columns.Add("fldCountryId", typeof(Guid));
        table.Rows.Add(Guid.NewGuid(), "GA1", "1 Group Street", countryId);
        connection.RespondToQuery("EXEC dbo.spgaGroupAddress", table);

        var result = await repository.GetGroupAddressesAsync();

        Assert.Single(result);
        Assert.Equal("GA1", result[0].Identifier);
        Assert.Equal(countryId, result[0].CountryId);
    }

    [Fact]
    public async Task GetSystemSettingsAsync_ReturnsMappedSettings()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldUTNumber", typeof(string));
        table.Rows.Add("UT1/1");
        connection.RespondToQuery("EXEC dbo.spgaSystemSettings", table);

        var result = await repository.GetSystemSettingsAsync();

        Assert.Equal("UT1/1", result.UTNumber);
    }

    [Fact]
    public async Task GetSchedulesAsync_ReturnsMappedSchedules()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldScheduleId", typeof(Guid));
        table.Columns.Add("fldSchedule", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Monthly");
        connection.RespondToQuery("EXEC dbo.spgaSchedule", table);

        var result = await repository.GetSchedulesAsync();

        Assert.Single(result);
        Assert.Equal("Monthly", result[0].Schedule);
    }

    [Fact]
    public async Task GetScheduleCodesAsync_ReturnsMappedScheduleCodes()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldScheduleCodeId", typeof(Guid));
        table.Columns.Add("fldScheduleCode", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "M");
        connection.RespondToQuery("EXEC dbo.spgaScheduleCode", table);

        var result = await repository.GetScheduleCodesAsync();

        Assert.Single(result);
        Assert.Equal("M", result[0].ScheduleCode);
    }

    [Fact]
    public async Task GetMonthlyDistributionsAsync_ReturnsMappedDistributions()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldMonthId", typeof(int));
        table.Rows.Add(2026, 4);
        connection.RespondToQuery("EXEC dbo.spgaMonthlyDistributionInfo", table);

        var result = await repository.GetMonthlyDistributionsAsync();

        Assert.Single(result);
        Assert.Equal(4, result[0].MonthId);
    }

    [Fact]
    public async Task GetDaysAsync_ReturnsMappedDays()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldDayId", typeof(Guid));
        table.Columns.Add("fldDay", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Monday");
        connection.RespondToQuery("EXEC dbo.spgaDay", table);

        var result = await repository.GetDaysAsync();

        Assert.Single(result);
        Assert.Equal("Monday", result[0].Day);
    }

    [Fact]
    public async Task GetPTNumbersAsync_ReturnsMappedIdentifiers()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Rows.Add(schemeId, "PT1234");
        connection.RespondToQuery("EXEC dbo.spgaPTNumbers", table);

        var result = await repository.GetPTNumbersAsync();

        Assert.Single(result);
        Assert.Equal("PT1234", result[0].Identifier);
    }

    [Fact]
    public async Task GetTestConsultantsAsync_MarksSecondResultSetAsExternal()
    {
        var (repository, connection) = CreateRepository();
        var internalId = Guid.NewGuid();
        var externalId = Guid.NewGuid();

        var internalTable = new DataTable();
        internalTable.Columns.Add("fldUserId", typeof(Guid));
        internalTable.Columns.Add("fldFriendlyName", typeof(string));
        internalTable.Columns.Add("fldIsInactive", typeof(bool));
        internalTable.Rows.Add(internalId, "Internal Consultant", false);

        var externalTable = new DataTable();
        externalTable.Columns.Add("fldUserId", typeof(Guid));
        externalTable.Columns.Add("fldFriendlyName", typeof(string));
        externalTable.Columns.Add("fldIsInactive", typeof(bool));
        externalTable.Rows.Add(externalId, "External Consultant", false);

        var dataSet = new DataSet();
        dataSet.Tables.Add(internalTable);
        dataSet.Tables.Add(externalTable);
        connection.RespondToQuery("EXEC dbo.spgaUserAllTestConsultant", dataSet);

        var result = await repository.GetTestConsultantsAsync();

        Assert.Equal(2, result.Count);
        Assert.False(result.Single(c => c.UserId == internalId).IsExternal);
        Assert.True(result.Single(c => c.UserId == externalId).IsExternal);
    }

    [Fact]
    public async Task GetAssessorsAsync_ReturnsMappedAssessors()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldUserId", typeof(Guid));
        table.Columns.Add("fldFriendlyName", typeof(string));
        table.Columns.Add("fldIsInactive", typeof(bool));
        table.Rows.Add(userId, "Assessor One", false);
        connection.RespondToQuery("EXEC dbo.spgaUserAssessor", table);

        var result = await repository.GetAssessorsAsync();

        Assert.Single(result);
        Assert.Equal("Assessor One", result[0].FriendlyName);
    }

    [Theory]
    [InlineData(SchemeItemTypeKind.TestType, "spgTestTypeByYearId")]
    [InlineData(SchemeItemTypeKind.TestResultItemType, "spgTestResultItemTypeByYearId")]
    [InlineData(SchemeItemTypeKind.TestMethodItemType, "spgTestMethodItemTypeByYearId")]
    [InlineData(SchemeItemTypeKind.CategoryItemType, "spgCategoryItemTypeByYearId")]
    [InlineData(SchemeItemTypeKind.CriterionItemType, "spgCriterionItemTypeByYearId")]
    public async Task GetSchemeItemTypesAsync_EachKind_CallsItsOwnProcedure(SchemeItemTypeKind kind, string procedure)
    {
        var (repository, connection) = CreateRepository();
        var itemTypeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldTestTypeId", typeof(Guid));
        table.Columns.Add("fldTestType", typeof(string));
        table.Columns.Add("fldNoLongerInUse", typeof(bool));
        table.Rows.Add(itemTypeId, "Antibody", false);
        connection.RespondToQuery($"EXEC dbo.{procedure} @YearId", table);

        var result = await repository.GetSchemeItemTypesAsync(kind, 2026);

        Assert.Single(result);
        Assert.Equal(itemTypeId, result[0].ItemTypeId);
    }

    [Fact]
    public async Task GetSchemeItemTypesAsync_UnknownKind_ThrowsArgumentOutOfRangeException()
    {
        var (repository, _) = CreateRepository();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetSchemeItemTypesAsync((SchemeItemTypeKind)999, 2026));
    }
}
