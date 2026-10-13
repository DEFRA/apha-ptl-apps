using PTL.Core.TestConsultant;

namespace PTL.Api.Tests.ExternalUser;

internal sealed class FakeExternalLoginService : IExternalLoginService
{
    public bool Success { get; set; } = true;
    public List<Guid> Calls { get; } = [];

    public Task<bool> GenerateLoginAsync(Guid externalTestConsultantId, string name, string email, CancellationToken cancellationToken = default)
    {
        Calls.Add(externalTestConsultantId);
        return Task.FromResult(Success);
    }
}
