using PTL.Core.Audit;

namespace PTL.Api.Tests.Audit;

internal sealed class FakeActingUserAccessor : IActingUserAccessor
{
    public Guid? ActingUserId { get; set; }
}
