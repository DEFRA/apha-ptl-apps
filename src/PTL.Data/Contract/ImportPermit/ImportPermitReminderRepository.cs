using Dapper;
using PTL.Core.Contract.ImportPermit;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.ImportPermit;

public sealed class ImportPermitReminderRepository(IDbConnectionFactory connectionFactory) : IImportPermitReminderRepository
{
    public async Task<ImportPermitReminderData> GetDueAsync(int daysBeforeDistribution, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgaContractOrderImportPermitForEmail @DaysBeforeDistribution",
            new { DaysBeforeDistribution = daysBeforeDistribution });

        // Result set order is fixed by the procedure: customers, then participants.
        var customers = (await results.ReadAsync<ImportPermitCustomerEntity>()).ToList();
        var participants = (await results.ReadAsync<ImportPermitParticipantEntity>()).ToList();
        return new ImportPermitReminderData(customers, participants);
    }

    public async Task MarkReminderSentAsync(
        Guid contractId,
        Guid customerId,
        Guid monthlyDistributionSchemeId,
        Guid? participantId,
        int reminderType,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiImportPermitReminderSent @ContractId, @CustomerId, @MonthlyDistributionSchemeId, @ParticipantId, @ReminderType",
            new
            {
                ContractId = contractId,
                CustomerId = customerId,
                MonthlyDistributionSchemeId = monthlyDistributionSchemeId,
                ParticipantId = participantId,
                ReminderType = reminderType,
            });
    }
}
