using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace PTL.Data.Tests.Fakes;

// Minimal ADO.NET command test double - resolves its canned DbDataReader / non-query row count
// from the owning FakeDbConnection, keyed by exact CommandText, so a single fake connection can
// answer every "EXEC dbo.spXxx" call a repository method makes (including the follow-up
// GetByIdAsync re-read that CreateAsync/UpdateAsync perform after an insert/update).
internal sealed class FakeDbCommand(FakeDbConnection connection) : DbCommand
{
    private readonly FakeDbParameterCollection _parameters = new();

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; } = connection;
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public object? ParameterValue(string name) => _parameters.ValueOf(name);

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery()
    {
        connection.ExecutedCommands.Add(this);
        return connection.ResolveNonQueryResult(this);
    }

    public override object? ExecuteScalar() => throw new NotSupportedException("Not used by any repository under test.");

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        connection.ExecutedCommands.Add(this);
        return connection.ResolveReader(this);
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteDbDataReader(behavior));

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(ExecuteNonQuery());
}
