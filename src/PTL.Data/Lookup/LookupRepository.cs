using Dapper;
using PTL.Contracts.Lookup;
using PTL.Core.Lookup;
using PTL.Data.Infrastructure;

namespace PTL.Data.Lookup;

public sealed class LookupRepository(IDbConnectionFactory connectionFactory) : ILookupRepository
{
    public async Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CountryEntity>("EXEC dbo.spgaCountry")).ToList();
    }

    public async Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CurrencyEntity>("EXEC dbo.spgaCurrency")).ToList();
    }

    public async Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CustomerTypeEntity>("EXEC dbo.spgaCustomerType")).ToList();
    }

    public async Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<VatRatingEntity>("EXEC dbo.spgaVatRating")).ToList();
    }

    public async Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<LabTypeEntity>("EXEC dbo.spgaLabType")).ToList();
    }

    public async Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<YearEntity>("EXEC dbo.spgaYearCurrent")).ToList();
    }

    public async Task<IReadOnlyList<YearEntity>> GetAllYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<YearEntity>("EXEC dbo.spgaYear")).ToList();
    }

    public async Task<IReadOnlyList<YearEntity>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<YearEntity>("EXEC dbo.spgaWeightedPricingYear")).ToList();
    }

    public async Task<IReadOnlyList<GroupAddressEntity>> GetGroupAddressesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<GroupAddressEntity>("EXEC dbo.spgaGroupAddress")).ToList();
    }

    public async Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<SchemeCurrencyEntity>("EXEC dbo.spgaSchemeCurrency")).ToList();
    }

    public async Task<IReadOnlyList<ScheduleEntity>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<ScheduleEntity>("EXEC dbo.spgaSchedule")).ToList();
    }

    public async Task<IReadOnlyList<ScheduleCodeEntity>> GetScheduleCodesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<ScheduleCodeEntity>("EXEC dbo.spgaScheduleCode")).ToList();
    }

    public async Task<IReadOnlyList<MonthlyDistributionEntity>> GetMonthlyDistributionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<MonthlyDistributionEntity>("EXEC dbo.spgaMonthlyDistributionInfo")).ToList();
    }

    public async Task<IReadOnlyList<DayEntity>> GetDaysAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<DayEntity>("EXEC dbo.spgaDay")).ToList();
    }

    public async Task<IReadOnlyList<PTNumberEntity>> GetPTNumbersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<PTNumberEntity>("EXEC dbo.spgaPTNumbers")).ToList();
    }

    public async Task<IReadOnlyList<SchemeUserEntity>> GetTestConsultantsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Result set 1 is internal users with the Test Consultant role, result set 2 is external
        // test consultants; both alias their key as fldUserId.
        using var results = await connection.QueryMultipleAsync("EXEC dbo.spgaUserAllTestConsultant");

        var internalConsultants = (await results.ReadAsync<SchemeUserEntity>()).ToList();
        var externalConsultants = (await results.ReadAsync<SchemeUserEntity>()).ToList();
        foreach (var consultant in externalConsultants)
        {
            consultant.IsExternal = true;
        }

        return [.. internalConsultants, .. externalConsultants];
    }

    public async Task<IReadOnlyList<SchemeUserEntity>> GetAssessorsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<SchemeUserEntity>("EXEC dbo.spgaUserAssessor")).ToList();
    }

    public async Task<IReadOnlyList<SchemeItemTypeEntity>> GetSchemeItemTypesAsync(SchemeItemTypeKind kind, int yearId, CancellationToken cancellationToken = default)
    {
        // Each branch is a complete, literal command text - no part of the SQL is built from a
        // runtime value, so there is nothing here for a SQL-injection scanner to flag.
        var sql = kind switch
        {
            SchemeItemTypeKind.TestType => "EXEC dbo.spgTestTypeByYearId @YearId",
            SchemeItemTypeKind.TestResultItemType => "EXEC dbo.spgTestResultItemTypeByYearId @YearId",
            SchemeItemTypeKind.TestMethodItemType => "EXEC dbo.spgTestMethodItemTypeByYearId @YearId",
            SchemeItemTypeKind.CategoryItemType => "EXEC dbo.spgCategoryItemTypeByYearId @YearId",
            SchemeItemTypeKind.CriterionItemType => "EXEC dbo.spgCriterionItemTypeByYearId @YearId",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown scheme item type."),
        };

        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<SchemeItemTypeEntity>(sql, new { YearId = yearId })).ToList();
    }

    public async Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<PostagePricingPlanEntity>(
            "EXEC dbo.spgPostageByYearID @YearId",
            new { YearId = yearId })).ToList();
    }

    public async Task<SystemSettingsEntity> GetSystemSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryFirstAsync<SystemSettingsEntity>("EXEC dbo.spgaSystemSettings");
    }
}
