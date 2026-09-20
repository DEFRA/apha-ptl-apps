using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data.Scheme;

// Wraps the existing spgSchemeBySchemeId / spiScheme / spuScheme / spgSchemeInfoByYearId /
// spgSchemeInfoBySharedId stored procedures via EF Core; the database schema and procedure
// behaviour are owned elsewhere and are not modified here. Parameter counts/order were verified
// against the live LocalDB schema (57 params for spiScheme/spuScheme) before wiring this up - see
// docs/analysis/scheme-analysis.md and repo memory notes on schema-drift risk.
public sealed class SchemeRepository(PtlDbContext dbContext) : ISchemeRepository
{
    public async Task<CoreScheme?> GetByIdAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        // EXEC ... is not composable SQL, so SingleOrDefaultAsync (which wraps the query) cannot be
        // used here. spgSchemeBySchemeId also returns two further result sets (SchemeCurrency, Test)
        // which are out of scope for Scheme Core - FromSqlRaw/ToListAsync only reads the first.
        var schemeIdParameter = new SqlParameter("@SchemeId", schemeId);

        var results = await dbContext.Schemes
            .FromSqlRaw("EXEC dbo.spgSchemeBySchemeId @SchemeId", schemeIdParameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        await dbContext.SchemeSummaries
            .FromSqlRaw("EXEC dbo.spgSchemeInfoByYearId @YearId", new SqlParameter("@YearId", yearId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SchemeHistoryEntity>> GetHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default) =>
        await dbContext.SchemeHistory
            .FromSqlRaw("EXEC dbo.spgSchemeInfoBySharedId @SharedId", new SqlParameter("@SharedId", sharedId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<CoreScheme> CreateAsync(CoreScheme scheme, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(InsertSql, BuildParameters(scheme), cancellationToken);
        var created = await GetByIdAsync(scheme.SchemeId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Scheme {scheme.SchemeId} was inserted but could not be re-read.");
    }

    public async Task<CoreScheme?> UpdateAsync(CoreScheme scheme, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(UpdateSql, BuildParameters(scheme), cancellationToken);
        return rowsAffected == 0 ? null : await GetByIdAsync(scheme.SchemeId, cancellationToken);
    }

    // Parameter order matches spiScheme/spuScheme exactly (confirmed against the live schema - 57
    // parameters, no drift). SampleNoSequence and IsReadOnly are never passed - both are
    // computed/joined by spgSchemeBySchemeId only.
    private const string InsertSql =
        "EXEC dbo.spiScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";

    private const string UpdateSql =
        "EXEC dbo.spuScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";

    private static SqlParameter[] BuildParameters(CoreScheme scheme) =>
    [
        new SqlParameter("@SchemeId", scheme.SchemeId),
        new SqlParameter("@SharedId", scheme.SharedId),
        new SqlParameter("@YearId", scheme.YearId),
        new SqlParameter("@Identifier", scheme.Identifier),
        new SqlParameter("@Name", scheme.Name),
        new SqlParameter("@ScheduleId", scheme.ScheduleId),
        new SqlParameter("@ScheduleCodeId", scheme.ScheduleCodeId),
        new SqlParameter("@StartDate", (object?)scheme.StartDate ?? DBNull.Value),
        new SqlParameter("@DistributionMonthJan", scheme.DistributionMonthJan),
        new SqlParameter("@DistributionMonthFeb", scheme.DistributionMonthFeb),
        new SqlParameter("@DistributionMonthMar", scheme.DistributionMonthMar),
        new SqlParameter("@DistributionMonthApr", scheme.DistributionMonthApr),
        new SqlParameter("@DistributionMonthMay", scheme.DistributionMonthMay),
        new SqlParameter("@DistributionMonthJun", scheme.DistributionMonthJun),
        new SqlParameter("@DistributionMonthJul", scheme.DistributionMonthJul),
        new SqlParameter("@DistributionMonthAug", scheme.DistributionMonthAug),
        new SqlParameter("@DistributionMonthSep", scheme.DistributionMonthSep),
        new SqlParameter("@DistributionMonthOct", scheme.DistributionMonthOct),
        new SqlParameter("@DistributionMonthNov", scheme.DistributionMonthNov),
        new SqlParameter("@DistributionMonthDec", scheme.DistributionMonthDec),
        new SqlParameter("@DistributionAsAvailable", scheme.DistributionAsAvailable),
        new SqlParameter("@WeekNumber", scheme.WeekNumber),
        new SqlParameter("@DayOfWeekId", scheme.DayOfWeekId),
        new SqlParameter("@Deadline", scheme.Deadline),
        new SqlParameter("@Pilot", scheme.Pilot),
        new SqlParameter("@Accredited", scheme.Accredited),
        new SqlParameter("@ComerciallyAvailable", scheme.ComerciallyAvailable),
        new SqlParameter("@LimitedSampleAvailability", scheme.LimitedSampleAvailability),
        new SqlParameter("@NoVLALabs", scheme.NoVLALabs),
        new SqlParameter("@CombinedPackaging", scheme.CombinedPackaging),
        new SqlParameter("@SampleOrigin", scheme.SampleOrigin),
        new SqlParameter("@Subcontractor", scheme.Subcontractor),
        new SqlParameter("@NumberOfSamples", scheme.NumberOfSamples),
        new SqlParameter("@SamplePackingInstructions", scheme.SamplePackingInstructions),
        new SqlParameter("@TestConsultant1", (object?)scheme.TestConsultant1 ?? DBNull.Value),
        new SqlParameter("@TestConsultant2", (object?)scheme.TestConsultant2 ?? DBNull.Value),
        new SqlParameter("@TestConsultant3", (object?)scheme.TestConsultant3 ?? DBNull.Value),
        new SqlParameter("@CommentsRequired", scheme.CommentsRequired),
        new SqlParameter("@DateOfReceipt", scheme.DateOfReceipt),
        new SqlParameter("@StorageConditions", scheme.StorageConditions),
        new SqlParameter("@ConditionOnReceipt", scheme.ConditionOnReceipt),
        new SqlParameter("@Instructions", scheme.Instructions),
        new SqlParameter("@TestConsultantTabulationId", (object?)scheme.TestConsultantTabulationId ?? DBNull.Value),
        new SqlParameter("@UseExternalReference", scheme.UseExternalReference),
        new SqlParameter("@LastModified", scheme.LastModified),
        new SqlParameter("@StoreRatings", scheme.StoreRatings),
        new SqlParameter("@Assessor1", (object?)scheme.Assessor1 ?? DBNull.Value),
        new SqlParameter("@Assessor2", (object?)scheme.Assessor2 ?? DBNull.Value),
        new SqlParameter("@Assessor3", (object?)scheme.Assessor3 ?? DBNull.Value),
        new SqlParameter("@Assessor4", (object?)scheme.Assessor4 ?? DBNull.Value),
        new SqlParameter("@RequiresAssessment", scheme.RequiresAssessment),
        new SqlParameter("@StandardTabulationText", (object?)scheme.StandardTabulationText ?? DBNull.Value),
        new SqlParameter("@Postage", (object?)scheme.Postage ?? DBNull.Value),
        new SqlParameter("@CustomsDocumentDescription", (object?)scheme.CustomsDescription ?? DBNull.Value),
        new SqlParameter("@CustomsDocumentVolume", (object?)scheme.CustomsVolume ?? DBNull.Value),
        new SqlParameter("@DataConsentDeclarationActive", scheme.DataConsentDeclarationActive),
        new SqlParameter("@DataConsentDeclarationText", (object?)scheme.DataConsentDeclarationText ?? DBNull.Value)
    ];
}
