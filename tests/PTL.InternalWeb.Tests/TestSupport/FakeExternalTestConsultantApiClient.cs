using PTL.ApiClient;
using PTL.Contracts.TestConsultant;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IExternalTestConsultantApiClient so SystemAdministrationController tests don't
// need a real HTTP call to PTL.Api.
internal sealed class FakeExternalTestConsultantApiClient : IExternalTestConsultantApiClient
{
    public IReadOnlyList<ExternalTestConsultantResponse> TestConsultants { get; set; } = [];
    public ExternalTestConsultantSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public ExternalTestConsultantResponse? SetStatusResult { get; set; }
    public GenerateLoginResponse GenerateLoginResult { get; set; } = new(true, null);
    public ExternalTestConsultantSaveRequest? LastCreateRequest { get; private set; }
    public List<(Guid ExternalTestConsultantId, ExternalTestConsultantSaveRequest Request)> UpdateCalls { get; } = [];
    public List<(Guid ExternalTestConsultantId, bool IsInactive)> SetStatusCalls { get; } = [];
    public List<Guid> GenerateLoginCalls { get; } = [];

    public Task<IReadOnlyList<ExternalTestConsultantResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TestConsultants);

    public Task<ExternalTestConsultantSaveResult> CreateAsync(ExternalTestConsultantSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<ExternalTestConsultantSaveResult> UpdateAsync(Guid externalTestConsultantId, ExternalTestConsultantSaveRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((externalTestConsultantId, request));
        return Task.FromResult(SaveResult);
    }

    public Task<ExternalTestConsultantResponse?> SetStatusAsync(Guid externalTestConsultantId, bool isInactive, CancellationToken cancellationToken = default)
    {
        SetStatusCalls.Add((externalTestConsultantId, isInactive));
        return Task.FromResult(SetStatusResult);
    }

    public Task<GenerateLoginResponse> GenerateLoginAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default)
    {
        GenerateLoginCalls.Add(externalTestConsultantId);
        return Task.FromResult(GenerateLoginResult);
    }
}
