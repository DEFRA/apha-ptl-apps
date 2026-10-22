using System.Net;
using System.Net.Http.Json;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient;

public enum TestConsultantApiStatus
{
    Ok,
    NotFound,

    // The signed-in user is not one of the scheme's Test Consultants.
    Forbidden,
}

public sealed record TestConsultantApiResult<T>(TestConsultantApiStatus Status, T? Value = default)
{
    public bool IsOk => Status == TestConsultantApiStatus.Ok;
}

public interface ITestConsultantTabulationApiClient
{
    Task<TestConsultantDistributionsResponse> GetDistributionsAsync(CancellationToken cancellationToken = default);

    Task<TestConsultantApiResult<TestConsultantTabulationResponse>> GetTabulationAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default);

    Task<TestConsultantApiResult<TabulationAttachmentFile>> ExportTabulationAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default);

    Task<TestConsultantApiResult<TestConsultantTabulationSaveResult>> SaveCommentsAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken = default);
}

// The Test Consultant is identified by the acting user header the ActingUserDelegatingHandler adds to every request.
public sealed class TestConsultantTabulationApiClient(HttpClient httpClient) : ITestConsultantTabulationApiClient
{
    private const string BasePath = "/api/test-consultant";

    public async Task<TestConsultantDistributionsResponse> GetDistributionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(new Uri($"{BasePath}/distributions", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TestConsultantDistributionsResponse>(cancellationToken)
            ?? new TestConsultantDistributionsResponse([]);
    }

    public async Task<TestConsultantApiResult<TestConsultantTabulationResponse>> GetTabulationAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
    {
        var url = $"{BasePath}/schemes/{monthlyDistributionSchemeId}/tabulation{FilterQuery(filter)}";
        return await ReadAsync<TestConsultantTabulationResponse>(await httpClient.GetAsync(new Uri(url, UriKind.Relative), cancellationToken), cancellationToken);
    }

    public async Task<TestConsultantApiResult<TabulationAttachmentFile>> ExportTabulationAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
    {
        var url = $"{BasePath}/schemes/{monthlyDistributionSchemeId}/tabulation/export{FilterQuery(filter)}";
        var response = await httpClient.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
        if (ToFailure<TabulationAttachmentFile>(response) is { } failure)
        {
            return failure;
        }

        return new TestConsultantApiResult<TabulationAttachmentFile>(
            TestConsultantApiStatus.Ok,
            new TabulationAttachmentFile(
                await response.Content.ReadAsByteArrayAsync(cancellationToken),
                response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
                response.Content.Headers.ContentDisposition?.ToString() ?? "attachment"));
    }

    public async Task<TestConsultantApiResult<TestConsultantTabulationSaveResult>> SaveCommentsAsync(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken = default) =>
        await ReadAsync<TestConsultantTabulationSaveResult>(
            await httpClient.PutAsJsonAsync($"{BasePath}/schemes/{monthlyDistributionSchemeId}/comments", request, cancellationToken),
            cancellationToken);

    private static string FilterQuery(TestConsultantTabulationFilter filter)
    {
        var parts = filter.HiddenSamples.Select(s => $"hiddenSamples={Uri.EscapeDataString(s)}").ToList();
        if (filter.HideMethodItems)
        {
            parts.Add("hideMethodItems=true");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }

    private static TestConsultantApiResult<T>? ToFailure<T>(HttpResponseMessage response)
        where T : class
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return new TestConsultantApiResult<T>(TestConsultantApiStatus.NotFound);
            case HttpStatusCode.Forbidden:
                return new TestConsultantApiResult<T>(TestConsultantApiStatus.Forbidden);
            default:
                response.EnsureSuccessStatusCode();
                return null;
        }
    }

    private static async Task<TestConsultantApiResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        if (ToFailure<T>(response) is { } failure)
        {
            return failure;
        }

        return new TestConsultantApiResult<T>(TestConsultantApiStatus.Ok, await response.Content.ReadFromJsonAsync<T>(cancellationToken));
    }
}
