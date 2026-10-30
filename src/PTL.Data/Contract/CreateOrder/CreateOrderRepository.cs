using Dapper;
using PTL.Core.Contract.CreateOrder;
using PTL.Core.Contract.PendingOrder;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.CreateOrder;

// Participant-facing pending-order procedures (legacy PendingContractOrder.aspx): spgPendingContractByCustomerID,
// spiPendingContract, spgaAvailableSchemeInfoByYearId, spgPastParticipantSchemeByParticipantSchemeId and
// spiPendingParticipantScheme.
public sealed class CreateOrderRepository(IDbConnectionFactory connectionFactory) : ICreateOrderRepository
{
    public async Task<PendingOrderEntity?> GetPendingOrderAsync(Guid customerId, int yearId, bool isSubmitted, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<PendingOrderEntity>(
            "EXEC dbo.spgPendingContractByCustomerID @CustomerId = @CustomerId, @IsSubmitted = @IsSubmitted, @YearId = @YearId",
            new { CustomerId = customerId, IsSubmitted = isSubmitted, YearId = yearId });
    }

    public async Task CreatePendingOrderAsync(Guid pendingContractId, Guid customerId, int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spiPendingContract @PendingContractId = @PendingContractId, @CustomerId = @CustomerId, @YearId = @YearId, @IsSubmitted = @IsSubmitted, @PurchaseOrderNumber = @PurchaseOrderNumber",
            new { PendingContractId = pendingContractId, CustomerId = customerId, YearId = yearId, IsSubmitted = false, PurchaseOrderNumber = string.Empty });
    }

    public async Task<IReadOnlyList<AvailableSchemeEntity>> GetAvailableSchemesAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<AvailableSchemeEntity>(
            "EXEC dbo.spgaAvailableSchemeInfoByYearId @YearId = @YearId",
            new { YearId = yearId })).ToList();
    }

    public async Task<IReadOnlyList<ParticipatingSchemeEntity>> GetParticipatingSchemesAsync(Guid participantId, int yearId, int participatingYearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<ParticipatingSchemeEntity>(
            "EXEC dbo.spgPastParticipantSchemeByParticipantSchemeId @participantId = @ParticipantId, @yearId = @YearId, @participatingYearId = @ParticipatingYearId",
            new { ParticipantId = participantId, YearId = yearId, ParticipatingYearId = participatingYearId })).ToList();
    }

    public async Task InsertSchemeAsync(PendingOrderSchemeEntity scheme, bool nonUk, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(InsertSchemeSql, new
        {
            scheme.PendingParticipantSchemeId,
            scheme.ParticipantId,
            scheme.SchemeId,
            scheme.PendingContractId,
            scheme.DistributionMonthJan,
            scheme.DistributionMonthFeb,
            scheme.DistributionMonthMar,
            scheme.DistributionMonthApr,
            scheme.DistributionMonthMay,
            scheme.DistributionMonthJun,
            scheme.DistributionMonthJul,
            scheme.DistributionMonthAug,
            scheme.DistributionMonthSep,
            scheme.DistributionMonthOct,
            scheme.DistributionMonthNov,
            scheme.DistributionMonthDec,
            NonUk = nonUk,
            scheme.IsRemoved,
            scheme.ImportExportLicenceRequired,
            scheme.IsSelected,
            scheme.DataConsentDeclarationGiven
        });
    }

    // Bound by name - the procedure takes 21 arguments.
    private const string InsertSchemeSql =
        "EXEC dbo.spiPendingParticipantScheme @PendingParticipantSchemeId = @PendingParticipantSchemeId, @ParticipantId = @ParticipantId, @SchemeId = @SchemeId, @PendingContractId = @PendingContractId, @DistributionMonthJan = @DistributionMonthJan, @DistributionMonthFeb = @DistributionMonthFeb, @DistributionMonthMar = @DistributionMonthMar, @DistributionMonthApr = @DistributionMonthApr, @DistributionMonthMay = @DistributionMonthMay, @DistributionMonthJun = @DistributionMonthJun, @DistributionMonthJul = @DistributionMonthJul, @DistributionMonthAug = @DistributionMonthAug, @DistributionMonthSep = @DistributionMonthSep, @DistributionMonthOct = @DistributionMonthOct, @DistributionMonthNov = @DistributionMonthNov, @DistributionMonthDec = @DistributionMonthDec, @NonUk = @NonUk, @IsRemoved = @IsRemoved, @ImportExportLicenceRequired = @ImportExportLicenceRequired, @IsSelected = @IsSelected, @DataConsentDeclarationGiven = @DataConsentDeclarationGiven";
}
