using PTL.ApiClient;
using PTL.Contracts.Participant;

namespace PTL.ExternalWeb.Tests.TestSupport;

internal sealed class FakeParticipantDetailsEditApiClient : IParticipantDetailsEditApiClient
{
    public ParticipantDetailsEditResponse? Details { get; set; }
    public ParticipantDetailsEditSaveResult SubmitResult { get; set; } = new(true, new Dictionary<string, string[]>());
    public ParticipantDetailsEditSaveRequest? LastSubmitRequest { get; private set; }

    public Task<ParticipantDetailsEditResponse?> GetAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Details);

    public Task<ParticipantDetailsEditSaveResult> SubmitAsync(Guid participantId, ParticipantDetailsEditSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastSubmitRequest = request;
        return Task.FromResult(SubmitResult);
    }
}
