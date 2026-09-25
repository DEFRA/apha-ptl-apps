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

    public async Task<ContractItemsAggregate?> GetContractItemsAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync("EXEC dbo.spgContractItems @ContractId", new { ContractId = contractId });

        var header = await multi.ReadSingleOrDefaultAsync();
        if (header is null)
        {
            return null;
        }

        var schemeRows = (await multi.ReadAsync()).ToList();
        var participantRows = (await multi.ReadAsync()).ToList();

        IDictionary<string, object> Row(dynamic row) => (IDictionary<string, object>)row;

        var headerRow = Row(header);
        var aggregate = new ContractItemsAggregate
        {
            ContractId = (Guid)headerRow["fldContractId"],
            Suffix = headerRow["fldSuffix"] as string ?? string.Empty,
            YearId = Convert.ToInt32(headerRow["fldYearId"]),
            QalNumber = headerRow["fldQalNumber"] as string ?? string.Empty,
            Symbol = headerRow["fldSymbol"] as string ?? string.Empty,
            DiscountRate = Convert.ToDecimal(headerRow["fldDiscountRate"]),
            AdministrationCharge = Convert.ToDecimal(headerRow["fldAdministrationCharge"]),
            NumberCourier = Convert.ToInt32(headerRow["fldNumberCourier"]),
            CourierPrice = Convert.ToDecimal(headerRow["fldCourierPrice"]),
            NumberPostage = Convert.ToInt32(headerRow["fldNumberPostage"]),
            PostagePrice = Convert.ToDecimal(headerRow["fldPostagePrice"]),
            NumberSpecialDelivery = Convert.ToInt32(headerRow["fldNumberSpecialDelivery"]),
            SpecialDeliveryPrice = Convert.ToDecimal(headerRow["fldSpecialDeliveryPrice"]),
            IsReadOnly = GetBool(headerRow, "fldIsReadOnly")
        };

        var schemesById = schemeRows.ToDictionary(
            s => (Guid)Row(s)["fldSchemeId"],
            s =>
            {
                var schemeRow = Row(s);
                return new ContractItemSchemeGroup
                {
                    SchemeId = (Guid)schemeRow["fldSchemeId"],
                    SchemeName = schemeRow["fldName"] as string ?? string.Empty,
                    SchemeIdentifier = schemeRow["fldIdentifier"] as string ?? string.Empty
                };
            });

        string[] overrideMonthColumns =
        [
            "fldIsOverrideJan", "fldIsOverrideFeb", "fldIsOverrideMar", "fldIsOverrideApr",
            "fldIsOverrideMay", "fldIsOverrideJun", "fldIsOverrideJul", "fldIsOverrideAug",
            "fldIsOverrideSep", "fldIsOverrideOct", "fldIsOverrideNov", "fldIsOverrideDec"
        ];

        foreach (var participantRow in participantRows)
        {
            var row = Row(participantRow);

            // Explicit-delete model (see docs/migration/contract-migration.md): once removed via
            // DELETE /api/contracts/{contractId}/items/{participantSchemeId}, an item no longer
            // appears in this read model at all - unlike legacy's deferred-delete "[Item Removed]" row.
            if (GetBool(row, "fldIsRemoved"))
            {
                continue;
            }

            var schemeId = (Guid)row["fldSchemeId"];
            if (!schemesById.TryGetValue(schemeId, out var group))
            {
                continue;
            }

            group.Participants.Add(new ContractItemLine
            {
                ParticipantSchemeId = (Guid)row["fldParticipantSchemeId"],
                ParticipantId = (Guid)row["fldParticipantId"],
                SchemeId = schemeId,
                LabCode = row["fldLabCode"] as string ?? string.Empty,
                LabName = row["fldLabName"] as string ?? string.Empty,
                NumberOfDistributions = Convert.ToInt32(row["fldNumberOfDistributions"]),
                Price = Convert.ToDecimal(row["fldPrice"]),
                NonFeePaying = GetBool(row, "fldNonFeePaying"),
                HasOverride = overrideMonthColumns.Any(column => GetBool(row, column))
            });
        }

        aggregate.Schemes = schemesById.Values
            .Where(g => g.Participants.Count > 0)
            .OrderBy(g => g.SchemeIdentifier, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return aggregate;
    }

    // NULL bit columns throw InvalidCastException on a direct (bool) cast (and Convert.ToBoolean
    // throws on DBNull too) - treats missing/null as false explicitly.
    private static bool GetBool(IDictionary<string, object> row, string column) =>
        row.TryGetValue(column, out var value) && value is bool flag && flag;
}

