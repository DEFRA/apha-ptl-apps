using System.Data;
using Dapper;
using PTL.Core.Scheme;
using PTL.Data.Infrastructure;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data.Scheme;

public sealed class SchemeRepository(IDbConnectionFactory connectionFactory) : ISchemeRepository
{
    public async Task<CoreScheme?> GetByIdAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<CoreScheme>(
            "EXEC dbo.spgSchemeBySchemeId @SchemeId",
            new { SchemeId = schemeId });
    }

    public async Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<SchemeSummaryEntity>(
            "EXEC dbo.spgSchemeInfoByYearId @YearId",
            new { YearId = yearId })).ToList();
    }

    public async Task<IReadOnlyList<SchemeHistoryEntity>> GetHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<SchemeHistoryEntity>(
            "EXEC dbo.spgSchemeInfoBySharedId @SharedId",
            new { SharedId = sharedId })).ToList();
    }

    public async Task<CoreScheme> CreateAsync(CoreScheme scheme, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(InsertSql, BuildParameters(scheme));
        var created = await GetByIdAsync(scheme.SchemeId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Scheme {scheme.SchemeId} was inserted but could not be re-read.");
    }

    public async Task<CoreScheme?> UpdateAsync(CoreScheme scheme, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(UpdateSql, BuildParameters(scheme));
        return rowsAffected == 0 ? null : await GetByIdAsync(scheme.SchemeId, cancellationToken);
    }

    private const string InsertSql =
        "EXEC dbo.spiScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";

    private const string UpdateSql =
        "EXEC dbo.spuScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";

    private static DynamicParameters BuildParameters(CoreScheme scheme)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@SchemeId", scheme.SchemeId);
        parameters.Add("@SharedId", scheme.SharedId);
        parameters.Add("@YearId", scheme.YearId);
        parameters.Add("@Identifier", scheme.Identifier);
        parameters.Add("@Name", scheme.Name);
        parameters.Add("@ScheduleId", scheme.ScheduleId);
        parameters.Add("@ScheduleCodeId", scheme.ScheduleCodeId);
        parameters.Add("@StartDate", (object?)scheme.StartDate ?? DBNull.Value);
        parameters.Add("@DistributionMonthJan", scheme.DistributionMonthJan);
        parameters.Add("@DistributionMonthFeb", scheme.DistributionMonthFeb);
        parameters.Add("@DistributionMonthMar", scheme.DistributionMonthMar);
        parameters.Add("@DistributionMonthApr", scheme.DistributionMonthApr);
        parameters.Add("@DistributionMonthMay", scheme.DistributionMonthMay);
        parameters.Add("@DistributionMonthJun", scheme.DistributionMonthJun);
        parameters.Add("@DistributionMonthJul", scheme.DistributionMonthJul);
        parameters.Add("@DistributionMonthAug", scheme.DistributionMonthAug);
        parameters.Add("@DistributionMonthSep", scheme.DistributionMonthSep);
        parameters.Add("@DistributionMonthOct", scheme.DistributionMonthOct);
        parameters.Add("@DistributionMonthNov", scheme.DistributionMonthNov);
        parameters.Add("@DistributionMonthDec", scheme.DistributionMonthDec);
        parameters.Add("@DistributionAsAvailable", scheme.DistributionAsAvailable);
        parameters.Add("@WeekNumber", scheme.WeekNumber);
        parameters.Add("@DayOfWeekId", scheme.DayOfWeekId);
        parameters.Add("@Deadline", scheme.Deadline);
        parameters.Add("@Pilot", scheme.Pilot);
        parameters.Add("@Accredited", scheme.Accredited);
        parameters.Add("@ComerciallyAvailable", scheme.ComerciallyAvailable);
        parameters.Add("@LimitedSampleAvailability", scheme.LimitedSampleAvailability);
        parameters.Add("@NoVLALabs", scheme.NoVLALabs);
        parameters.Add("@CombinedPackaging", scheme.CombinedPackaging);
        parameters.Add("@SampleOrigin", scheme.SampleOrigin);
        parameters.Add("@Subcontractor", scheme.Subcontractor);
        parameters.Add("@NumberOfSamples", scheme.NumberOfSamples);
        parameters.Add("@SamplePackingInstructions", scheme.SamplePackingInstructions);
        parameters.Add("@TestConsultant1", (object?)scheme.TestConsultant1 ?? DBNull.Value);
        parameters.Add("@TestConsultant2", (object?)scheme.TestConsultant2 ?? DBNull.Value);
        parameters.Add("@TestConsultant3", (object?)scheme.TestConsultant3 ?? DBNull.Value);
        parameters.Add("@CommentsRequired", scheme.CommentsRequired);
        parameters.Add("@DateOfReceipt", scheme.DateOfReceipt);
        parameters.Add("@StorageConditions", scheme.StorageConditions);
        parameters.Add("@ConditionOnReceipt", scheme.ConditionOnReceipt);
        parameters.Add("@Instructions", scheme.Instructions);
        parameters.Add("@TestConsultantTabulationId", (object?)scheme.TestConsultantTabulationId ?? DBNull.Value);
        parameters.Add("@UseExternalReference", scheme.UseExternalReference);
        parameters.Add("@LastModified", scheme.LastModified);
        parameters.Add("@StoreRatings", scheme.StoreRatings);
        parameters.Add("@Assessor1", (object?)scheme.Assessor1 ?? DBNull.Value);
        parameters.Add("@Assessor2", (object?)scheme.Assessor2 ?? DBNull.Value);
        parameters.Add("@Assessor3", (object?)scheme.Assessor3 ?? DBNull.Value);
        parameters.Add("@Assessor4", (object?)scheme.Assessor4 ?? DBNull.Value);
        parameters.Add("@RequiresAssessment", scheme.RequiresAssessment);
        parameters.Add("@StandardTabulationText", (object?)scheme.StandardTabulationText ?? DBNull.Value);
        parameters.Add("@Postage", (object?)scheme.Postage ?? DBNull.Value);
        parameters.Add("@CustomsDocumentDescription", (object?)scheme.CustomsDescription ?? DBNull.Value);
        parameters.Add("@CustomsDocumentVolume", (object?)scheme.CustomsVolume ?? DBNull.Value);
        parameters.Add("@DataConsentDeclarationActive", scheme.DataConsentDeclarationActive);
        parameters.Add("@DataConsentDeclarationText", (object?)scheme.DataConsentDeclarationText ?? DBNull.Value);
        return parameters;
    }
}
