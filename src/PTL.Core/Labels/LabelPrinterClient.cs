using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PTL.Core.Labels;

// Sends the composed label straight to the configured network printer - the "raw/JetDirect" port
// 9100 protocol, so no OS print driver/spooler is required (unavailable on this app's Linux
// container hosting). The label is formatted as ZPL (ZplLabelFormatter) before being written to the
// socket - the PC43 is a ZPL-capable printer and will not render plain text sent over the raw port.
// Mirrors legacy LabelBase.Print()'s own shape: attempt the print, log the outcome, return a plain
// success/failure bool for the caller to show a confirmation message from - never throws for a
// printing failure, only for a null label.
public sealed class LabelPrinterClient(IOptions<LabelPrinterOptions> options, ILogger<LabelPrinterClient> logger) : ILabelPrinterClient
{
    public async Task<bool> PrintAsync(AddressLabel label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(label);

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            logger.LogError("LabelPrinter:Host is not configured - cannot print label");
            return false;
        }

        try
        {
            using var client = new TcpClient();
            using var connectTimeout = new CancellationTokenSource(settings.ConnectTimeoutMilliseconds);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectTimeout.Token);

            await client.ConnectAsync(settings.Host, settings.Port, linkedSource.Token);

            await using var stream = client.GetStream();

            var payload = ZplLabelFormatter.ToZpl(label);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            logger.LogError(ex, "Error whilst printing label to {Host}:{Port}", settings.Host, settings.Port);
            return false;
        }
    }
}
