using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public enum AssessorApiStatus
{
    Ok,
    NotFound,

    // The signed-in user is not one of the scheme's Assessors.
    Forbidden,
}

public sealed record AssessorApiResult<T>(AssessorApiStatus Status, T? Value = default)
{
    public bool IsOk => Status == AssessorApiStatus.Ok;
}

public enum AssessorSaveStatus
{
    Saved,
    NotFound,

    // The signed-in user is not one of the scheme's Assessors.
    Forbidden,

    // The test has been submitted, so its assessment can no longer change.
    Submitted,

    // The API refused the values; Errors name the fields.
    Invalid,
}

// Key names the field in the form ("Comment", "OverallScore", "Categories[0].Score").
public sealed record AssessorFieldError(string Key, string Message);

public sealed record AssessorSaveApiResult(AssessorSaveStatus Status, IReadOnlyList<AssessorFieldError> Errors)
{
    public static AssessorSaveApiResult Of(AssessorSaveStatus status) => new(status, []);
}

public enum AssessorSubmitStatus
{
    Submitted,
    NotFound,

    // The signed-in user is not one of the scheme's Assessors.
    Forbidden,

    // The test has already been submitted.
    AlreadySubmitted,
}

public interface IAssessorApiClient
{
    Task<AssessorApiResult<AssessorMenuResponse>> GetMenuAsync(int? year, CancellationToken cancellationToken = default);

    Task<AssessorApiResult<AssessorItemListResponse>> GetItemListAsync(Guid testId, CancellationToken cancellationToken = default);

    Task<AssessorApiResult<AssessorEntryResponse>> GetEntryAsync(Guid sampleTestId, Guid participantId, CancellationToken cancellationToken = default);

    Task<AssessorSaveApiResult> SaveEntryAsync(Guid sampleTestId, Guid participantId, AssessorEntrySaveRequest request, CancellationToken cancellationToken = default);

    Task<AssessorSubmitStatus> SubmitAsync(Guid testId, CancellationToken cancellationToken = default);
}

// The Assessor is identified by the acting user header the ActingUserDelegatingHandler adds to every request.
public sealed class AssessorApiClient(HttpClient httpClient) : IAssessorApiClient
{
    private const string BasePath = "/api/assessor";

    public Task<AssessorApiResult<AssessorMenuResponse>> GetMenuAsync(int? year, CancellationToken cancellationToken = default) =>
        GetAsync<AssessorMenuResponse>(year is { } value ? $"{BasePath}/menu?year={value}" : $"{BasePath}/menu", cancellationToken);

    public Task<AssessorApiResult<AssessorItemListResponse>> GetItemListAsync(Guid testId, CancellationToken cancellationToken = default) =>
        GetAsync<AssessorItemListResponse>($"{BasePath}/tests/{testId}/items", cancellationToken);

    public Task<AssessorApiResult<AssessorEntryResponse>> GetEntryAsync(Guid sampleTestId, Guid participantId, CancellationToken cancellationToken = default) =>
        GetAsync<AssessorEntryResponse>($"{BasePath}/entries/{sampleTestId}/{participantId}", cancellationToken);

    public async Task<AssessorSaveApiResult> SaveEntryAsync(Guid sampleTestId, Guid participantId, AssessorEntrySaveRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(new Uri($"{BasePath}/entries/{sampleTestId}/{participantId}", UriKind.Relative), request, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return AssessorSaveApiResult.Of(AssessorSaveStatus.NotFound);
            case HttpStatusCode.Forbidden:
                return AssessorSaveApiResult.Of(AssessorSaveStatus.Forbidden);
            case HttpStatusCode.Conflict:
                return AssessorSaveApiResult.Of(AssessorSaveStatus.Submitted);
            case HttpStatusCode.BadRequest:
                var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
                return new AssessorSaveApiResult(
                    AssessorSaveStatus.Invalid,
                    [.. (problem?.Errors ?? new Dictionary<string, string[]>()).SelectMany(field => field.Value.Select(message => new AssessorFieldError(field.Key, message)))]);
            default:
                response.EnsureSuccessStatusCode();
                return AssessorSaveApiResult.Of(AssessorSaveStatus.Saved);
        }
    }

    public async Task<AssessorSubmitStatus> SubmitAsync(Guid testId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(new Uri($"{BasePath}/tests/{testId}/submit", UriKind.Relative), content: null, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return AssessorSubmitStatus.NotFound;
            case HttpStatusCode.Forbidden:
                return AssessorSubmitStatus.Forbidden;
            case HttpStatusCode.Conflict:
                return AssessorSubmitStatus.AlreadySubmitted;
            default:
                response.EnsureSuccessStatusCode();
                return AssessorSubmitStatus.Submitted;
        }
    }

    private async Task<AssessorApiResult<T>> GetAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await httpClient.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return new AssessorApiResult<T>(AssessorApiStatus.NotFound);
            case HttpStatusCode.Forbidden:
                return new AssessorApiResult<T>(AssessorApiStatus.Forbidden);
            default:
                response.EnsureSuccessStatusCode();
                return new AssessorApiResult<T>(AssessorApiStatus.Ok, await response.Content.ReadFromJsonAsync<T>(cancellationToken));
        }
    }
}
