using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using CoreContract = PTL.Core.Contract.Contract;

namespace PTL.Data.Contract;

// Wraps the existing spgContractByContractId / spiContract / spuContract / spgContractInfoByCustomerId
// / spgContractInfoByCustomerIdAndYearId stored procedures via EF Core; the database schema and
// procedure behaviour are owned elsewhere and are not modified here. Parameter counts/order were
// verified against the live LocalDB schema (29 params for spiContract/spuContract) before wiring
// this up - see docs/analysis/contract-analysis.md and repo memory notes on schema-drift risk.
public sealed class ContractRepository(PtlDbContext dbContext) : IContractRepository
{
    public async Task<CoreContract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        // EXEC ... is not composable SQL, so SingleOrDefaultAsync (which wraps the query) cannot be used here.
        var contractIdParameter = new SqlParameter("@ContractId", contractId);

        var results = await dbContext.Contracts
            .FromSqlRaw("EXEC dbo.spgContractByContractId @ContractId", contractIdParameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesAsync(Guid customerId, ContractPeriodFilter period, CancellationToken cancellationToken = default)
    {
        var activeParameter = new SqlParameter("@Active", SqlDbType.TinyInt) { Value = (byte)period };

        return await dbContext.ContractSummaries
            .FromSqlRaw("EXEC dbo.spgContractInfoByCustomerId @CustomerId, @Active", new SqlParameter("@CustomerId", customerId), activeParameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ContractSummaries
            .FromSqlRaw("EXEC dbo.spgContractInfoByCustomerIdAndYearId @CustomerId, @YearId", new SqlParameter("@CustomerId", customerId), new SqlParameter("@YearId", yearId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<CoreContract> CreateAsync(CoreContract contract, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(InsertSql, BuildParameters(contract), cancellationToken);
        var created = await GetByIdAsync(contract.ContractId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Contract {contract.ContractId} was inserted but could not be re-read.");
    }

    public async Task<CoreContract?> UpdateAsync(CoreContract contract, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(UpdateSql, BuildParameters(contract), cancellationToken);
        return rowsAffected == 0 ? null : await GetByIdAsync(contract.ContractId, cancellationToken);
    }

    // Parameter order matches spiContract/spuContract exactly (see ProficiencyTestingDatabase/Object
    // Scripts/Stored Procedures/spi/spiContract.sql and spu/spuContract.sql - 29 parameters each,
    // confirmed against the live schema). CustomerName, QalNumber, and IsReadOnly are never passed -
    // they are computed/joined by spgContractByContractId only.
    private const string InsertSql =
        "EXEC dbo.spiContract @ContractId, @CustomerId, @YearId, @UTNumber, @FTNumber, @ContractSignatory, @ActionsRequired, @RenewalInformation, @DiscountRate, @AdministrationCharge, @NumberCourier, @CourierPrice, @NumberPostage, @PostagePrice, @NumberSpecialDelivery, @SpecialDeliveryPrice, @AcknowledgementPostedDate, @AcknowledgementReturnedDate, @JobSheetPostedDate, @ReasonForClosure, @DateOfLeaving, @IsActive, @Suffix, @CommencementDate, @PurchaseOrderNumber, @OptOutOfInvoiceGeneration, @IsOnlineOrder, @ApprovedBy, @ApprovedDate";

    private const string UpdateSql =
        "EXEC dbo.spuContract @ContractId, @CustomerId, @YearId, @UTNumber, @FTNumber, @ContractSignatory, @ActionsRequired, @RenewalInformation, @DiscountRate, @AdministrationCharge, @NumberCourier, @CourierPrice, @NumberPostage, @PostagePrice, @NumberSpecialDelivery, @SpecialDeliveryPrice, @AcknowledgementPostedDate, @AcknowledgementReturnedDate, @JobSheetPostedDate, @ReasonForClosure, @DateOfLeaving, @IsActive, @Suffix, @CommencementDate, @PurchaseOrderNumber, @OptOutOfInvoiceGeneration, @IsOnlineOrder, @ApprovedBy, @ApprovedDate";

    private static SqlParameter[] BuildParameters(CoreContract contract) =>
    [
        new SqlParameter("@ContractId", contract.ContractId),
        new SqlParameter("@CustomerId", contract.CustomerId),
        new SqlParameter("@YearId", contract.YearId),
        new SqlParameter("@UTNumber", contract.UTNumber),
        new SqlParameter("@FTNumber", contract.FTNumber),
        new SqlParameter("@ContractSignatory", contract.ContractSignatory),
        new SqlParameter("@ActionsRequired", contract.ActionsRequired),
        new SqlParameter("@RenewalInformation", contract.RenewalInformation),
        new SqlParameter("@DiscountRate", contract.DiscountRate),
        new SqlParameter("@AdministrationCharge", contract.AdministrationCharge),
        new SqlParameter("@NumberCourier", contract.NumberCourier),
        new SqlParameter("@CourierPrice", contract.CourierPrice),
        new SqlParameter("@NumberPostage", contract.NumberPostage),
        new SqlParameter("@PostagePrice", contract.PostagePrice),
        new SqlParameter("@NumberSpecialDelivery", contract.NumberSpecialDelivery),
        new SqlParameter("@SpecialDeliveryPrice", contract.SpecialDeliveryPrice),
        new SqlParameter("@AcknowledgementPostedDate", (object?)contract.AcknowledgementPostedDate ?? DBNull.Value),
        new SqlParameter("@AcknowledgementReturnedDate", (object?)contract.AcknowledgementReturnedDate ?? DBNull.Value),
        new SqlParameter("@JobSheetPostedDate", (object?)contract.JobSheetPostedDate ?? DBNull.Value),
        new SqlParameter("@ReasonForClosure", contract.ReasonForClosure),
        new SqlParameter("@DateOfLeaving", (object?)contract.DateOfLeaving ?? DBNull.Value),
        new SqlParameter("@IsActive", contract.IsActive),
        new SqlParameter("@Suffix", contract.Suffix),
        new SqlParameter("@CommencementDate", (object?)contract.CommencementDate ?? DBNull.Value),
        new SqlParameter("@PurchaseOrderNumber", contract.PurchaseOrderNumber),
        new SqlParameter("@OptOutOfInvoiceGeneration", contract.OptOutOfInvoiceGeneration),
        new SqlParameter("@IsOnlineOrder", contract.IsOnlineOrder),
        new SqlParameter("@ApprovedBy", (object?)contract.ApprovedBy ?? DBNull.Value),
        new SqlParameter("@ApprovedDate", (object?)contract.ApprovedDate ?? DBNull.Value)
    ];
}
