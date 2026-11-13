using PTL.ApiClient;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeLabelPrinterApiClient : ILabelPrinterApiClient
{
    public bool PrintResult { get; set; } = true;
    public List<IReadOnlyList<string>> PrintedLines { get; } = [];

    public Task<bool> PrintAsync(IReadOnlyList<string> lines, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        PrintedLines.Add(lines);
        return Task.FromResult(PrintResult);
    }
}
