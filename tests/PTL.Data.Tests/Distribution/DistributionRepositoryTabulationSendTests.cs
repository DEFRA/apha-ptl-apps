using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// Legacy MonthlyDistribution.Save -> each changed scheme's Update, in one transaction.
public class DistributionRepositoryTabulationSendTests
{
    private const string ResultsReadySql = """
        EXEC dbo.spuMonthlyDistributionSchemeForResultsReady
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @IsReadyForTestConsultant = @IsReadyForTestConsultant,
            @IsReadyForResultsSignOff = @IsReadyForResultsSignOff,
            @IsReadyForAssessor = @IsReadyForAssessor
        """;
    private const string SubmitCommentSql = "EXEC dbo.sppSubmitComment @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string SignOffSql = "EXEC dbo.spuTabulationForSignOff @DistributionTabulationId = @TabulationId, @PublishState = @PublishState";

    private static readonly Guid SchemeId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid OtherSchemeId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid TabulationId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid OtherTabulationId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly DateTime Tc = new(2026, 5, 1);
    private static readonly DateTime SignOff = new(2026, 5, 2);
    private static readonly DateTime Assessor = new(2026, 5, 3);

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        // The SQL is matched exactly, so register both line-ending forms of the multi-line text.
        foreach (var sql in new[] { ResultsReadySql, SubmitCommentSql, SignOffSql })
        {
            connection.RespondToNonQuery(sql.ReplaceLineEndings("\n"), 1);
            connection.RespondToNonQuery(sql.ReplaceLineEndings("\r\n"), 1);
        }

        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static string Normalise(string sql) => sql.ReplaceLineEndings("\n");

    [Fact]
    public async Task SendTabulationSchemesAsync_NullWrites_Throws()
    {
        var (repository, _) = CreateRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SendTabulationSchemesAsync(null!));
    }

    [Fact]
    public async Task SendTabulationSchemesAsync_Empty_CommitsWithNoCommands()
    {
        var (repository, connection) = CreateRepository();

        await repository.SendTabulationSchemesAsync([]);

        Assert.Empty(connection.ExecutedCommands);
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task SendTabulationSchemesAsync_RunsEachSchemesUpdatesInOrderInOneTransaction()
    {
        var (repository, connection) = CreateRepository();

        await repository.SendTabulationSchemesAsync(
        [
            new TabulationSendWrite(SchemeId, Tc, SignOff, Assessor, true, [(TabulationId, 2), (OtherTabulationId, 3)]),
            new TabulationSendWrite(OtherSchemeId, null, null, null, false, []),
        ]);

        var transaction = connection.LastTransaction!;
        Assert.True(transaction.Committed);
        Assert.False(transaction.RolledBack);
        Assert.Equal(
            [Normalise(ResultsReadySql), SubmitCommentSql, SignOffSql, SignOffSql, Normalise(ResultsReadySql)],
            connection.ExecutedCommands.Select(c => Normalise(c.CommandText)));
        Assert.All(connection.ExecutedCommands, c => Assert.Same(transaction, c.Transaction));

        var first = connection.ExecutedCommands[0];
        Assert.Equal((SchemeId, Tc, SignOff, Assessor),
            (first.ParameterValue("MonthlyDistributionSchemeId"), first.ParameterValue("IsReadyForTestConsultant"),
                first.ParameterValue("IsReadyForResultsSignOff"), first.ParameterValue("IsReadyForAssessor")));
        Assert.Equal(SchemeId, connection.ExecutedCommands[1].ParameterValue("MonthlyDistributionSchemeId"));
        Assert.Equal((TabulationId, 2), (connection.ExecutedCommands[2].ParameterValue("TabulationId"), connection.ExecutedCommands[2].ParameterValue("PublishState")));
        Assert.Equal((OtherTabulationId, 3), (connection.ExecutedCommands[3].ParameterValue("TabulationId"), connection.ExecutedCommands[3].ParameterValue("PublishState")));

        var last = connection.ExecutedCommands[4];
        Assert.Equal(OtherSchemeId, last.ParameterValue("MonthlyDistributionSchemeId"));
        Assert.All(["IsReadyForTestConsultant", "IsReadyForResultsSignOff", "IsReadyForAssessor"],
            name => Assert.True(last.ParameterValue(name) is null or DBNull));
    }
}
