using System.Data;
using Dapper;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Data.Infrastructure;
using CoreContract = PTL.Core.Contract.Contract;

namespace PTL.Data.Contract;

// The stored procedure contracts are unchanged; the repository executes them through Dapper
// against a connection obtained from IDbConnectionFactory.
public sealed class ContractRepository(IDbConnectionFactory connectionFactory) : IContractRepository
{
    public async Task<CoreContract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<CoreContract>(
            "EXEC dbo.spgContractByContractId @ContractId",
            new { ContractId = contractId });
    }

    public async Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesAsync(Guid customerId, ContractPeriodFilter period, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<ContractSummaryEntity>(
            "EXEC dbo.spgContractInfoByCustomerId @CustomerId, @Active",
            new { CustomerId = customerId, Active = (byte)period })).ToList();
    }

    public async Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<ContractSummaryEntity>(
            "EXEC dbo.spgContractInfoByCustomerIdAndYearId @CustomerId, @YearId",
            new { CustomerId = customerId, YearId = yearId })).ToList();
    }

    public async Task<CoreContract> CreateAsync(CoreContract contract, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(InsertSql, BuildParameters(contract));
        var created = await GetByIdAsync(contract.ContractId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Contract {contract.ContractId} was inserted but could not be re-read.");
    }

    public async Task<CoreContract?> UpdateAsync(CoreContract contract, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(UpdateSql, BuildParameters(contract));
        return rowsAffected == 0 ? null : await GetByIdAsync(contract.ContractId, cancellationToken);
    }

    private const string InsertSql =
        "EXEC dbo.spiContract @ContractId, @CustomerId, @YearId, @UTNumber, @FTNumber, @ContractSignatory, @ActionsRequired, @RenewalInformation, @DiscountRate, @AdministrationCharge, @NumberCourier, @CourierPrice, @NumberPostage, @PostagePrice, @NumberSpecialDelivery, @SpecialDeliveryPrice, @AcknowledgementPostedDate, @AcknowledgementReturnedDate, @JobSheetPostedDate, @ReasonForClosure, @DateOfLeaving, @IsActive, @Suffix, @CommencementDate, @PurchaseOrderNumber, @OptOutOfInvoiceGeneration, @IsOnlineOrder, @ApprovedBy, @ApprovedDate";

    private const string UpdateSql =
        "EXEC dbo.spuContract @ContractId, @CustomerId, @YearId, @UTNumber, @FTNumber, @ContractSignatory, @ActionsRequired, @RenewalInformation, @DiscountRate, @AdministrationCharge, @NumberCourier, @CourierPrice, @NumberPostage, @PostagePrice, @NumberSpecialDelivery, @SpecialDeliveryPrice, @AcknowledgementPostedDate, @AcknowledgementReturnedDate, @JobSheetPostedDate, @ReasonForClosure, @DateOfLeaving, @IsActive, @Suffix, @CommencementDate, @PurchaseOrderNumber, @OptOutOfInvoiceGeneration, @IsOnlineOrder, @ApprovedBy, @ApprovedDate";

    private static DynamicParameters BuildParameters(CoreContract contract)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ContractId", contract.ContractId);
        parameters.Add("@CustomerId", contract.CustomerId);
        parameters.Add("@YearId", contract.YearId);
        parameters.Add("@UTNumber", contract.UTNumber);
        parameters.Add("@FTNumber", contract.FTNumber);
        parameters.Add("@ContractSignatory", contract.ContractSignatory);
        parameters.Add("@ActionsRequired", contract.ActionsRequired);
        parameters.Add("@RenewalInformation", contract.RenewalInformation);
        parameters.Add("@DiscountRate", contract.DiscountRate);
        parameters.Add("@AdministrationCharge", contract.AdministrationCharge);
        parameters.Add("@NumberCourier", contract.NumberCourier);
        parameters.Add("@CourierPrice", contract.CourierPrice);
        parameters.Add("@NumberPostage", contract.NumberPostage);
        parameters.Add("@PostagePrice", contract.PostagePrice);
        parameters.Add("@NumberSpecialDelivery", contract.NumberSpecialDelivery);
        parameters.Add("@SpecialDeliveryPrice", contract.SpecialDeliveryPrice);
        parameters.Add("@AcknowledgementPostedDate", contract.AcknowledgementPostedDate);
        parameters.Add("@AcknowledgementReturnedDate", contract.AcknowledgementReturnedDate);
        parameters.Add("@JobSheetPostedDate", contract.JobSheetPostedDate);
        parameters.Add("@ReasonForClosure", contract.ReasonForClosure);
        parameters.Add("@DateOfLeaving", contract.DateOfLeaving);
        parameters.Add("@IsActive", contract.IsActive);
        parameters.Add("@Suffix", contract.Suffix);
        parameters.Add("@CommencementDate", contract.CommencementDate);
        parameters.Add("@PurchaseOrderNumber", contract.PurchaseOrderNumber);
        parameters.Add("@OptOutOfInvoiceGeneration", contract.OptOutOfInvoiceGeneration);
        parameters.Add("@IsOnlineOrder", contract.IsOnlineOrder);
        parameters.Add("@ApprovedBy", contract.ApprovedBy);
        parameters.Add("@ApprovedDate", contract.ApprovedDate);
        return parameters;
    }
}
