using PTL.ApiClient;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeTestConsultantTabulationApiClient : ITestConsultantTabulationApiClient
{
    public TestConsultantDistributionsResponse Distributions { get; set; } = new([]);

    public Exception? DistributionsException { get; set; }

    public TestConsultantApiResult<TestConsultantTabulationResponse> Tabulation { get; set; } = new(TestConsultantApiStatus.NotFound);

    public TestConsultantApiResult<TestConsultantTabulationSaveResult> SaveResult { get; set; } =
        new(TestConsultantApiStatus.Ok, new TestConsultantTabulationSaveResult(true, false, new Dictionary<string, string>()));

    public TestConsultantApiResult<TabulationAttachmentFile> Export { get; set; } = new(TestConsultantApiStatus.NotFound);

    public List<TestConsultantTabulationFilter> TabulationFilters { get; } = [];

    public List<TestConsultantTabulationFilter> ExportFilters { get; } = [];

    public List<(Guid SchemeId, TestConsultantTabulationSaveRequest Request)> Saved { get; } = [];

    public Task<TestConsultantDistributionsResponse> GetDistributionsAsync(CancellationToken cancellationToken = default) =>
        DistributionsException is null ? Task.FromResult(Distributions) : Task.FromException<TestConsultantDistributionsResponse>(DistributionsException);

    public Task<TestConsultantApiResult<TestConsultantTabulationResponse>> GetTabulationAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
    {
        TabulationFilters.Add(filter);
        return Task.FromResult(Tabulation);
    }

    public Task<TestConsultantApiResult<TabulationAttachmentFile>> ExportTabulationAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
    {
        ExportFilters.Add(filter);
        return Task.FromResult(Export);
    }

    public Task<TestConsultantApiResult<TestConsultantTabulationSaveResult>> SaveCommentsAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken = default)
    {
        Saved.Add((monthlyDistributionSchemeId, request));
        return Task.FromResult(SaveResult);
    }
}
