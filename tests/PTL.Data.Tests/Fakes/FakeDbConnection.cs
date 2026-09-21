using System.Data;
using System.Data.Common;

namespace PTL.Data.Tests.Fakes;

// Minimal ADO.NET connection test double standing in for a real SQL Server connection so
// repositories (which only ever call CreateConnection/CreateCommand/ExecuteReaderAsync/
// ExecuteNonQueryAsync via Dapper) can be unit tested. Responses are configured per exact
// CommandText via RespondToQuery/RespondToNonQuery before exercising the repository method under
// test; ExecutedCommands records everything actually sent, for assertions on the SQL/parameters a
// repository built.
internal sealed class FakeDbConnection : DbConnection
{
    private readonly Dictionary<string, Func<FakeDbCommand, DbDataReader>> _readerFactories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<FakeDbCommand, int>> _nonQueryResults = new(StringComparer.OrdinalIgnoreCase);
    private ConnectionState _state = ConnectionState.Closed;

    public List<FakeDbCommand> ExecutedCommands { get; } = [];

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "FakeDatabase";
    public override string DataSource => "FakeDataSource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    // Configures the DataTable/DataTableReader a QuerySingleOrDefaultAsync/QueryAsync call
    // against this exact "EXEC dbo.spXxx ..." command text should return.
    public void RespondToQuery(string commandText, DataTable table) =>
        _readerFactories[commandText] = _ => table.CreateDataReader();

    // Configures the rows-affected count an ExecuteAsync (insert/update) call against this exact
    // command text should return.
    public void RespondToNonQuery(string commandText, int rowsAffected) =>
        _nonQueryResults[commandText] = _ => rowsAffected;

    internal DbDataReader ResolveReader(FakeDbCommand command) =>
        _readerFactories.TryGetValue(command.CommandText, out var factory)
            ? factory(command)
            : throw new InvalidOperationException($"No reader configured for command text: '{command.CommandText}'.");

    internal int ResolveNonQueryResult(FakeDbCommand command) =>
        _nonQueryResults.TryGetValue(command.CommandText, out var factory)
            ? factory(command)
            : throw new InvalidOperationException($"No non-query result configured for command text: '{command.CommandText}'.");

    public override void Open() => _state = ConnectionState.Open;

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName) =>
        throw new NotSupportedException("Not used by any repository under test.");

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException("No repository under test uses transactions.");

    protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);

    // Repositories create a new connection per method call via IDbConnectionFactory and dispose it
    // at the end of a `using` block - this fake connection is reused across every call within a
    // test (its FakeDbConnectionFactory always returns the same instance), so disposal must not
    // discard the configured responses, only reset the open/closed state like a real pooled connection.
    protected override void Dispose(bool disposing)
    {
        Close();
        base.Dispose(disposing);
    }
}
