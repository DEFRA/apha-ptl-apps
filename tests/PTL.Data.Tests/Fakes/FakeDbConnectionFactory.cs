using PTL.Data.Infrastructure;

namespace PTL.Data.Tests.Fakes;

// Always returns the same FakeDbConnection instance - matches how repositories call
// IDbConnectionFactory.CreateConnection() once per method and Dispose it at the end of a `using`
// block, while letting a single test configure responses for every "EXEC dbo.spXxx" call a
// repository method (and any follow-up GetByIdAsync re-read) makes.
internal sealed class FakeDbConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
{
    public System.Data.IDbConnection CreateConnection() => connection;
}
