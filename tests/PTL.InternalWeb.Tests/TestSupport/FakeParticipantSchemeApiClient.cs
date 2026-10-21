using PTL.ApiClient;
using PTL.Contracts.Participant;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IParticipantSchemeApiClient so ParticipantSchemeController tests don't need a
// real HTTP call to PTL.Api.
internal sealed class FakeParticipantSchemeApiClient : IParticipantSchemeApiClient
{
    public ParticipantSchemeResponse? ParticipantSchemeResponse { get; set; }
    public ParticipantSchemeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());

    // Set to make Create/Update throw, exercising the controller's save-error catch blocks.
    public Exception? ExceptionToThrow { get; set; }

    public Task<ParticipantSchemeResponse?> GetParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ParticipantSchemeResponse);

    // What a new Contract Item learns about its Scheme (months already posted / initialised).
    public ParticipantSchemeContextResponse Context { get; set; } = new(Enumerable.Repeat(true, 12).ToArray(), []);

    public Task<ParticipantSchemeContextResponse> GetParticipantSchemeContextAsync(Guid contractId, Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Context);

    public Task<ParticipantSchemeSaveResult> CreateParticipantSchemeAsync(CreateParticipantSchemeRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalled = true;
        LastCreateRequest = request;
        return ExceptionToThrow is not null ? Task.FromException<ParticipantSchemeSaveResult>(ExceptionToThrow) : Task.FromResult(SaveResult);
    }

    public Task<ParticipantSchemeSaveResult> UpdateParticipantSchemeAsync(Guid participantSchemeId, UpdateParticipantSchemeRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalled = true;
        LastUpdateRequest = request;
        return ExceptionToThrow is not null ? Task.FromException<ParticipantSchemeSaveResult>(ExceptionToThrow) : Task.FromResult(SaveResult);
    }

    public bool CreateCalled { get; private set; }

    public bool UpdateCalled { get; private set; }

    public CreateParticipantSchemeRequest? LastCreateRequest { get; private set; }

    public UpdateParticipantSchemeRequest? LastUpdateRequest { get; private set; }

    public Task<ParticipantSchemeSaveResult> DeleteParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);
}
