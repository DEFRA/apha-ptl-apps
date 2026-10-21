using System.Data;
using Dapper;
using PTL.Core.Participant;

namespace PTL.Data.Participant;

// Legacy ParticipantScheme.UpdateDistributions: keeps the monthly distributions' participant lists
// in step with a contract item. Runs inside the caller's transaction (legacy was not transactional).
public sealed partial class ParticipantSchemeRepository
{
    private const string MismatchMessage =
        "The requested database update would result in a mismatch of customers between the contract and the participant record. Please double check the information and then try again.";

    private const int DefaultContractStartMonth = 4;

    private static async Task SyncDistributionsAsync(IDbConnection connection, IDbTransaction transaction, ParticipantSchemeRecord record)
    {
        var contractYearId = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT fldYearId FROM tblContract WHERE fldContractId = @ContractId",
            new { record.ContractId },
            transaction);
        if (contractYearId is null)
        {
            return;
        }

        var settings = await connection.QueryFirstOrDefaultAsync("EXEC dbo.spgaSystemSettings", transaction: transaction);
        var contractStartMonth = settings is IDictionary<string, object> settingsRow && settingsRow.TryGetValue("fldContractStartDate", out var start) && start is DateTime startDate
            ? startDate.Month
            : DefaultContractStartMonth;

        for (var monthId = 1; monthId <= ParticipantSchemeMonths.Count; monthId++)
        {
            var year = ParticipantSchemeDistributionPlanner.DistributionYear(contractYearId.Value, monthId, contractStartMonth);
            var schemes = await ReadMonthAsync(connection, transaction, year, monthId);
            if (schemes is null)
            {
                continue;
            }

            var plan = ParticipantSchemeDistributionPlanner.PlanMonth(record, monthId, schemes);

            foreach (var distributionParticipantId in plan.RemoveDistributionParticipantIds)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spdDistributionParticipant @DistributionParticipantId",
                    new { DistributionParticipantId = distributionParticipantId },
                    transaction);
            }

            var added = new List<Guid>();
            foreach (var monthlyDistributionSchemeId in plan.AddToMonthlyDistributionSchemeIds)
            {
                if (await AddParticipantAsync(connection, transaction, record, monthlyDistributionSchemeId))
                {
                    added.Add(monthlyDistributionSchemeId);
                }
            }

            foreach (var monthlyDistributionSchemeId in added)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiSetupSampleReceipt @MonthlyDistributionSchemeId",
                    new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId },
                    transaction);
            }
        }
    }

    // Null when the month has not been initialised (no tblMonthlyDistribution row).
    private static async Task<IReadOnlyList<DistributionSchemeSnapshot>?> ReadMonthAsync(IDbConnection connection, IDbTransaction transaction, int yearId, int monthId)
    {
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId",
            new { YearId = yearId, MonthId = monthId },
            transaction);

        var header = (await results.ReadAsync()).Cast<IDictionary<string, object>>().FirstOrDefault();
        if (header is null || header["fldMonthlyDistributionId"] is not Guid id || id == Guid.Empty)
        {
            return null;
        }

        var schemeRows = (await results.ReadAsync()).Cast<IDictionary<string, object>>().ToList();
        var participantRows = (await results.ReadAsync()).Cast<IDictionary<string, object>>().ToList();

        return [.. schemeRows.Select(s =>
        {
            var schemeId = (Guid)s["fldMonthlyDistributionSchemeId"];
            return new DistributionSchemeSnapshot(
                schemeId,
                (Guid)s["fldSchemeId"],
                s["fldDistributionReferenceSuffix"] as string,
                [.. participantRows
                    .Where(p => (Guid)p["fldDistributionSchemeId"] == schemeId)
                    .Select(p => new DistributionParticipantSnapshot((Guid)p["fldDistributionParticipantId"], (Guid)p["fldParticipantId"], (Guid)p["fldContractId"]))]);
        })];
    }

    private static async Task<bool> AddParticipantAsync(IDbConnection connection, IDbTransaction transaction, ParticipantSchemeRecord record, Guid monthlyDistributionSchemeId)
    {
        // spgMonthlyDistribution hides override-month participants, so one added by an earlier save is
        // invisible to the plan; without this a re-save would insert it again.
        var alreadyOnDistribution = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM tlnkDistributionParticipant WHERE fldParticipantId = @ParticipantId AND fldDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { record.ParticipantId, MonthlyDistributionSchemeId = monthlyDistributionSchemeId },
            transaction);
        if (alreadyOnDistribution > 0)
        {
            return false;
        }

        var mismatches = await connection.QueryAsync(
            "EXEC dbo.spgCheckForMissmatchContract @ParticipantId, @ContractId",
            new { record.ParticipantId, record.ContractId },
            transaction);
        if (mismatches.Any())
        {
            throw new ParticipantSchemeValidationException([new ParticipantSchemeValidationError(string.Empty, MismatchMessage)]);
        }

        await connection.ExecuteAsync(
            InsertDistributionParticipantSql,
            new { DistributionParticipantId = Guid.NewGuid(), record.ParticipantId, MonthlyDistributionSchemeId = monthlyDistributionSchemeId },
            transaction);
        return true;
    }

    // A new participant has no deadline override, exactly as legacy DistributionParticipant.NewParticipant.
    private const string InsertDistributionParticipantSql =
        "EXEC dbo.spiDistributionParticipant @DistributionParticipantId = @DistributionParticipantId, @ParticipantId = @ParticipantId, " +
        "@MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId, @DeadlineDate = NULL, @DeadlineChangeReason = '', @AdditionalParticipant = 0";
}
