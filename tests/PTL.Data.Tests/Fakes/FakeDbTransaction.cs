using System.Data;
using System.Data.Common;

namespace PTL.Data.Tests.Fakes;

// Minimal ADO.NET transaction test double. InvoiceRepository.MarkInvoicedAndRecordAuditAsync is
// the first repository in this layer to use an explicit transaction (mirroring legacy's
// TransactionScope) - this lets FakeDbConnection.BeginTransaction() hand back something a
// repository can pass to ExecuteAsync(transaction:) and Commit(), with Committed/RolledBack
// recorded for test assertions.
internal sealed class FakeDbTransaction(FakeDbConnection connection, IsolationLevel isolationLevel) : DbTransaction
{
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }

    protected override DbConnection DbConnection => connection;

    // Recorded as requested, so a test can assert the isolation level a repository asked for -
    // CreateDistributionStructureAsync depends on Serializable to make its re-check safe.
    public override IsolationLevel IsolationLevel => isolationLevel;

    public override void Commit() => Committed = true;

    public override void Rollback() => RolledBack = true;
}
