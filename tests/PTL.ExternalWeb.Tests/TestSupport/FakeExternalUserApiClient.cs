using PTL.ApiClient;
using PTL.Contracts.ExternalUser;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Test double for IExternalUserApiClient so ExternalUserResolver and lab selection tests don't need a real HTTP call.
internal sealed class FakeExternalUserApiClient(ResolveExternalUserResponse response, Exception? thrownException = null) : IExternalUserApiClient
{
    public ResolveExternalUserRequest? ReceivedRequest { get; private set; }

    /// <summary>The labs <see cref="GetLabsAsync"/> returns.</summary>
    public List<ExternalLab> Labs { get; } = [];

    /// <summary>The labs <see cref="GetLabAsync"/> accepts; any other lab is answered with null, as PTL.Api does.</summary>
    public List<ExternalLabContext> LabContexts { get; } = [];

    public List<Guid> GetLabsContactIds { get; } = [];

    public List<(Guid ContactId, Guid ParticipantId)> GetLabCalls { get; } = [];

    public Task<ResolveExternalUserResponse> ResolveAsync(ResolveExternalUserRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedRequest = request;
        return thrownException is not null
            ? throw thrownException
            : Task.FromResult(response);
    }

    public Task<IReadOnlyList<ExternalLab>> GetLabsAsync(Guid ssoIdExt, CancellationToken cancellationToken = default)
    {
        GetLabsContactIds.Add(ssoIdExt);
        return Task.FromResult<IReadOnlyList<ExternalLab>>(Labs.ToList());
    }

    public Task<ExternalLabContext?> GetLabAsync(Guid ssoIdExt, Guid participantId, CancellationToken cancellationToken = default)
    {
        GetLabCalls.Add((ssoIdExt, participantId));
        return Task.FromResult(LabContexts.FirstOrDefault(lab => lab.ParticipantId == participantId));
    }
}
