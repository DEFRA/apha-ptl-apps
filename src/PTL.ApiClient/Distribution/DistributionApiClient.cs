using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

// Thin typed HttpClient wrapper around PTL.Api's Distribution Dashboard endpoints.
public sealed class DistributionApiClient(HttpClient httpClient) : IDistributionApiClient
{
    public async Task<IReadOnlyList<DistributionDashboardMonthResponse>> GetDashboardAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var months = await httpClient.GetFromJsonAsync<IReadOnlyList<DistributionDashboardMonthResponse>>(
            $"/api/distributions/dashboard?year={yearId}", cancellationToken);
        return months ?? [];
    }

    public async Task<IReadOnlyList<DistributionYearOptionResponse>> GetYearsAsync(CancellationToken cancellationToken = default)
    {
        var years = await httpClient.GetFromJsonAsync<IReadOnlyList<DistributionYearOptionResponse>>(
            "/api/distributions/years", cancellationToken);
        return years ?? [];
    }

    public async Task<MonthlyDistributionResponse?> GetScheduleAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<MonthlyDistributionResponse>(
            $"/api/distributions/months/{yearId}/{monthId}/schedule", cancellationToken);

    public async Task<MonthlyDistributionScheduleSaveResult?> SaveScheduleAsync(
        int yearId, int monthId, IReadOnlyList<MonthlyDistributionScheduleRowRequest> rows, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/distributions/months/{yearId}/{monthId}/schedule", rows, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MonthlyDistributionScheduleSaveResult>(cancellationToken);
    }

    public async Task<SampleNumbersPageResult> GetSampleNumbersAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/schemes/{monthlyDistributionSchemeId}/sample-numbers", UriKind.Relative), cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new SampleNumbersPageResult(null, []);
        }

        // The structure could not be built from the Scheme - surface the business message rather
        // than failing with an opaque error.
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.SelectMany(e => e.Value).ToList()
                : ["Sample Numbers could not be opened for this distribution."];
            return new SampleNumbersPageResult(null, errors);
        }

        response.EnsureSuccessStatusCode();
        return new SampleNumbersPageResult(await response.Content.ReadFromJsonAsync<SampleNumbersResponse>(cancellationToken), []);
    }

    public async Task<SampleNumbersSaveResult?> SaveSampleNumbersAsync(
        Guid monthlyDistributionSchemeId, SampleNumbersSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/sample-numbers", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SampleNumbersSaveResult>(cancellationToken);
    }

    public async Task<IntendedResultsResponse?> GetIntendedResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/schemes/{monthlyDistributionSchemeId}/intended-results", UriKind.Relative), cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IntendedResultsResponse>(cancellationToken);
    }

    public async Task<IntendedResultsSaveResult?> SaveIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, IntendedResultsSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/intended-results", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IntendedResultsSaveResult>(cancellationToken);
    }

    public async Task<DistributionParticipantsResponse?> GetParticipantsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/schemes/{monthlyDistributionSchemeId}/participants", UriKind.Relative), cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DistributionParticipantsResponse>(cancellationToken);
    }

    public async Task<ChangeDeadlineResult?> ChangeDeadlineAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, ChangeDeadlineRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/participants/{distributionParticipantId}/deadline", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChangeDeadlineResult>(cancellationToken);
    }

    public async Task<RepeatDistributionResult?> RepeatDistributionAsync(
        Guid monthlyDistributionSchemeId, RepeatDistributionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/repeat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RepeatDistributionResult>(cancellationToken);
    }

    public async Task<PreparationSheetResponse?> GetPreparationAsync(int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/months/{yearId}/{monthId}/preparation", UriKind.Relative), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PreparationSheetResponse>(cancellationToken);
    }

    public async Task<PreparationSaveResult?> SavePreparationAsync(
        int yearId, int monthId, PreparationSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/distributions/months/{yearId}/{monthId}/preparation", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PreparationSaveResult>(cancellationToken);
    }

    public async Task<ResultsReceiptResponse?> GetResultsReceiptAsync(int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/months/{yearId}/{monthId}/results-receipt", UriKind.Relative), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResultsReceiptResponse>(cancellationToken);
    }

    public async Task<ResultsReceiptSaveResult?> SaveResultsReceiptAsync(
        int yearId, int monthId, ResultsReceiptSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/distributions/months/{yearId}/{monthId}/results-receipt", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResultsReceiptSaveResult>(cancellationToken);
    }

    public async Task<ActualResultsResponse?> GetActualResultsAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/schemes/{monthlyDistributionSchemeId}/participants/{distributionParticipantId}/actual-results", UriKind.Relative),
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActualResultsResponse>(cancellationToken);
    }

    public async Task<ActualResultsSaveResult?> SaveActualResultsAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, ActualResultsSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/participants/{distributionParticipantId}/actual-results", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActualResultsSaveResult>(cancellationToken);
    }

    public async Task<PackagingSheetResponse?> GetPackagingAsync(int yearId, int monthId, int mode, int week, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/months/{yearId}/{monthId}/packaging?mode={mode}&week={week}", UriKind.Relative), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PackagingSheetResponse>(cancellationToken);
    }

    public async Task<PackagingSaveResult?> SavePackagingAsync(
        int yearId, int monthId, PackagingSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/distributions/months/{yearId}/{monthId}/packaging", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PackagingSaveResult>(cancellationToken);
    }

    public async Task<PackagingLabelsResponse?> GetPackagingLabelsAsync(
        int yearId, int monthId, PackagingSelectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/distributions/months/{yearId}/{monthId}/packaging/labels", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PackagingLabelsResponse>(cancellationToken);
    }

    public async Task<PackagingCustomsResult> GetPackagingCustomsAsync(
        int yearId, int monthId, PackagingSelectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/distributions/months/{yearId}/{monthId}/packaging/customs", request, cancellationToken);

        // Legacy surfaced the business message (e.g. "Please select schemes to print") on the page.
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(cancellationToken);
            return new PackagingCustomsResult(null, string.Empty, problem?.Detail ?? "There was a problem with the Mail Merge");
        }

        response.EnsureSuccessStatusCode();
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "Customs.docx";
        return new PackagingCustomsResult(await response.Content.ReadAsByteArrayAsync(cancellationToken), fileName, null);
    }
}
