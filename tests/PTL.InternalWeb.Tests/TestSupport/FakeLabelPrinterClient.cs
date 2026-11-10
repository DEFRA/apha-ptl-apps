using PTL.Core.Labels;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeLabelPrinterClient : ILabelPrinterClient
{
    public bool PrintResult { get; set; } = true;
    public List<AddressLabel> PrintedLabels { get; } = [];

    public Task<bool> PrintAsync(AddressLabel label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(label);
        PrintedLabels.Add(label);
        return Task.FromResult(PrintResult);
    }
}
