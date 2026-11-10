using PTL.ApiClient;
using PTL.Contracts.LoginInstructions;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ILoginInstructionsApiClient so controller tests don't need a real HTTP call to PTL.Api.
internal sealed class FakeLoginInstructionsApiClient : ILoginInstructionsApiClient
{
    public LoginInstructionsResponse Result { get; set; } = new(true);
    public List<Guid> ParticipantCalls { get; } = [];
    public List<Guid> ViewerCalls { get; } = [];
    public List<Guid> ExternalTestConsultantCalls { get; } = [];

    public Task<LoginInstructionsResponse> SendToParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        ParticipantCalls.Add(participantId);
        return Task.FromResult(Result);
    }

    public Task<LoginInstructionsResponse> SendToViewerAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        ViewerCalls.Add(viewerId);
        return Task.FromResult(Result);
    }

    public Task<LoginInstructionsResponse> SendToExternalTestConsultantAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default)
    {
        ExternalTestConsultantCalls.Add(externalTestConsultantId);
        return Task.FromResult(Result);
    }
}
