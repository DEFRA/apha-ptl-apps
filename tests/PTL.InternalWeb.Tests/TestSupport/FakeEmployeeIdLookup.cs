using PTL.Common.Graph;
using PTL.InternalWeb.Infrastructure;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeEmployeeIdLookup : IEmployeeIdLookup
{
    public string? EmployeeId { get; init; }

    public bool Throw { get; init; }

    public Guid? RequestedObjectId { get; private set; }

    public Task<string?> FindEmployeeIdAsync(Guid objectId, CancellationToken cancellationToken)
    {
        RequestedObjectId = objectId;

        return Throw ? Task.FromException<string?>(new GraphException("down")) : Task.FromResult(EmployeeId);
    }
}
