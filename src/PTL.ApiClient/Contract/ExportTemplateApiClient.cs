using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PTL.Contracts.Contract;

namespace PTL.ApiClient;

public interface IExportTemplateApiClient
{
    Task<ExportTemplateListResponse?> GetTemplatesAsync(string documentType, CancellationToken cancellationToken = default);

    Task<ExportTemplateUploadResponse> UploadTemplateAsync(string documentType, string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Legacy "Open". Returns null when the template or its stored file is gone.</summary>
    Task<ExportTemplateDownload?> DownloadTemplateAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task<bool> SelectTemplateAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task<bool> DeleteTemplateAsync(Guid fileId, CancellationToken cancellationToken = default);
}

public sealed record ExportTemplateDownload(string FileName, string ContentType, byte[] Content);

public sealed class ExportTemplateApiClient(HttpClient httpClient) : IExportTemplateApiClient
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public async Task<ExportTemplateListResponse?> GetTemplatesAsync(string documentType, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/export-templates/{Uri.EscapeDataString(documentType)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExportTemplateListResponse>(cancellationToken);
    }

    public async Task<ExportTemplateUploadResponse> UploadTemplateAsync(string documentType, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(DocxContentType);
        form.Add(fileContent, "file", fileName);

        var response = await httpClient.PostAsync($"/api/export-templates/{Uri.EscapeDataString(documentType)}", form, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ExportTemplateUploadResponse(false, "File not found", null);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExportTemplateUploadResponse>(cancellationToken)
            ?? new ExportTemplateUploadResponse(false, "File could not be saved", null);
    }

    public async Task<ExportTemplateDownload?> DownloadTemplateAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/export-templates/{fileId}/content", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "template.docx";

        return new ExportTemplateDownload(
            fileName,
            response.Content.Headers.ContentType?.MediaType ?? DocxContentType,
            await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    public async Task<bool> SelectTemplateAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/api/export-templates/{fileId}/select", null, cancellationToken);
        return await SucceededAsync(response);
    }

    public async Task<bool> DeleteTemplateAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/export-templates/{fileId}", cancellationToken);
        return await SucceededAsync(response);
    }

    private static Task<bool> SucceededAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Task.FromResult(false);
        }

        response.EnsureSuccessStatusCode();
        return Task.FromResult(true);
    }
}
