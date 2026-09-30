using Dapper;
using PTL.Core.Contract.PendingOrder;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.PendingOrder;

// Legacy pending-order stored procedures: spgaPendingContracts,
// spgPendingContractByPendingContractId, spgPendingParticipantSchemeByPendingContractId,
// spuPendingParticipantSchemeByPendingParticipantSchemeId,
// spdPendingParticipantSchemeByPendingParticipantSchemeId, spuPendingContractByPendingContractID,
// spdPendingContractByPendingContractID.
public sealed class PendingOrderRepository(IDbConnectionFactory connectionFactory) : IPendingOrderRepository
{
    public async Task<IReadOnlyList<PendingOrderSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<PendingOrderSummaryEntity>(
            "EXEC dbo.spgaPendingContracts")).ToList();
    }

    public async Task<PendingOrderEntity?> GetByIdAsync(Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<PendingOrderEntity>(
            "EXEC dbo.spgPendingContractByPendingContractId @PendingContractID",
            new { PendingContractID = pendingContractId });
    }

    public async Task<IReadOnlyList<PendingOrderSchemeEntity>> GetSchemesAsync(Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<PendingOrderSchemeEntity>(
            "EXEC dbo.spgPendingParticipantSchemeByPendingContractId @PendingContractId",
            new { PendingContractId = pendingContractId })).ToList();
    }

    public async Task<PendingOrderSchemeEntity?> GetSchemeByIdAsync(Guid pendingParticipantSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<PendingOrderSchemeEntity>(
            "EXEC dbo.spgPendingParticipantSchemeByPendingParticipantSchemeId @PendingParticipantSchemeId = @PendingParticipantSchemeId",
            new { PendingParticipantSchemeId = pendingParticipantSchemeId });
    }

    public async Task UpdateSchemeAsync(PendingOrderSchemeEntity scheme, bool dataConsentDeclarationGiven, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(UpdateSchemeSql, new
        {
            scheme.PendingParticipantSchemeId,
            DistributionMonthJan = scheme.DistributionMonthJan,
            DistributionMonthFeb = scheme.DistributionMonthFeb,
            DistributionMonthMar = scheme.DistributionMonthMar,
            DistributionMonthApr = scheme.DistributionMonthApr,
            DistributionMonthMay = scheme.DistributionMonthMay,
            DistributionMonthJun = scheme.DistributionMonthJun,
            DistributionMonthJul = scheme.DistributionMonthJul,
            DistributionMonthAug = scheme.DistributionMonthAug,
            DistributionMonthSep = scheme.DistributionMonthSep,
            DistributionMonthOct = scheme.DistributionMonthOct,
            DistributionMonthNov = scheme.DistributionMonthNov,
            DistributionMonthDec = scheme.DistributionMonthDec,
            scheme.IsRemoved,
            scheme.ImportExportLicenceRequired,
            scheme.IsSelected,
            DataConsentDeclarationGiven = dataConsentDeclarationGiven
        });
    }

    public async Task DeleteSchemeAsync(Guid pendingParticipantSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spdPendingParticipantSchemeByPendingParticipantSchemeId @PendingParticipantSchemeId",
            new { PendingParticipantSchemeId = pendingParticipantSchemeId });
    }

    public async Task UpdatePurchaseOrderNumberAsync(Guid pendingContractId, string purchaseOrderNumber, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var existing = await GetByIdAsync(pendingContractId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        // @IsSubmitted must stay 1 - passing 0 would clear the submitted flag, and passing 1 makes
        // the procedure re-stamp fldOrderSubmitDate, which matches legacy's own Save() behaviour.
        // Bound by name: the procedure declares @PurchaseOrderNumber BEFORE @IsDeleted, so a
        // positional EXEC silently passes the varchar into the bit parameter.
        await connection.ExecuteAsync(
            "EXEC dbo.spuPendingContractByPendingContractID @PendingContractId = @PendingContractId, @YearId = @YearId, @IsSubmitted = @IsSubmitted, @PurchaseOrderNumber = @PurchaseOrderNumber, @IsDeleted = @IsDeleted",
            new
            {
                PendingContractId = pendingContractId,
                existing.YearId,
                IsSubmitted = existing.IsSubmitted,
                IsDeleted = existing.IsDeleted,
                PurchaseOrderNumber = purchaseOrderNumber
            });
    }

    public async Task<bool> MarkDecidedAsync(Guid customerId, Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // spdPendingContractByPendingContractID sets NOCOUNT ON, so ExecuteAsync reports -1 rather
        // than a row count. Success is confirmed by re-reading the order's IsDeleted flag instead.
        await connection.ExecuteAsync(
            "EXEC dbo.spdPendingContractByPendingContractID @CustomerId, @PendingContractId",
            new { CustomerId = customerId, PendingContractId = pendingContractId });

        var order = await GetByIdAsync(pendingContractId, cancellationToken);
        return order is null || order.IsDeleted;
    }

    // Bound by name rather than position - this procedure takes 17 arguments, so a silent ordering
    // mismatch would be both easy to introduce and hard to spot.
    private const string UpdateSchemeSql =
        "EXEC dbo.spuPendingParticipantSchemeByPendingParticipantSchemeId @PendingParticipantSchemeId = @PendingParticipantSchemeId, @DistributionMonthJan = @DistributionMonthJan, @DistributionMonthFeb = @DistributionMonthFeb, @DistributionMonthMar = @DistributionMonthMar, @DistributionMonthApr = @DistributionMonthApr, @DistributionMonthMay = @DistributionMonthMay, @DistributionMonthJun = @DistributionMonthJun, @DistributionMonthJul = @DistributionMonthJul, @DistributionMonthAug = @DistributionMonthAug, @DistributionMonthSep = @DistributionMonthSep, @DistributionMonthOct = @DistributionMonthOct, @DistributionMonthNov = @DistributionMonthNov, @DistributionMonthDec = @DistributionMonthDec, @IsRemoved = @IsRemoved, @ImportExportLicenceRequired = @ImportExportLicenceRequired, @IsSelected = @IsSelected, @DataConsentDeclarationGiven = @DataConsentDeclarationGiven";
}
