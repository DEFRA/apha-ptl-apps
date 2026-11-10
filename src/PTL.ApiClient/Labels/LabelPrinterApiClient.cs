using System.Net.Http.Json;
using PTL.Contracts.Labels;

namespace PTL.ApiClient;

// Printer interaction lives in PTL.Api, not here - this is a thin HTTP call, same shape as every
// other typed client in this project. See PTL.Api.Controllers.LabelPrintController and
// PTL.Core.Labels.ILabelPrinterClient for where the label actually reaches the printer.
public interface ILabelPrinterApiClient
{
    Task<bool> PrintAsync(IReadOnlyList<string> lines, CancellationToken cancellationToken = default);
}

public sealed class LabelPrinterApiClient(HttpClient httpClient) : ILabelPrinterApiClient
{
    public async Task<bool> PrintAsync(IReadOnlyList<string> lines, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var response = await httpClient.PostAsJsonAsync("/api/labels/print", new LabelPrintRequest(lines), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<LabelPrintResponse>(cancellationToken);
        return result?.Printed ?? false;
    }
}
