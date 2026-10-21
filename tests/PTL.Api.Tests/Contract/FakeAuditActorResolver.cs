using PTL.Core.Audit;

namespace PTL.Api.Tests.Contract;

internal sealed class FakeAuditActorResolver(string name = "tester") : IAuditActorResolver
{
    public Task<string> GetAuditNameAsync(CancellationToken cancellationToken = default) => Task.FromResult(name);
}
