using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PTL.Core.Labels;

// Sends the composed label straight to the configured network printer - the "raw/JetDirect" port
// 9100 protocol, so no OS print driver/spooler is required (unavailable on this app's Linux
// container hosting). The label is formatted as Fingerprint (FingerprintLabelFormatter) before being written to the
// socket - the PC43t's command language is Fingerprint, so plain text sent over the raw port would not print as a label.
// Mirrors legacy LabelBase.Print()'s own shape: attempt the print, log the outcome, return a plain
// success/failure bool for the caller to show a confirmation message from - never throws for a
// printing failure, only for a null label.
public sealed class LabelPrinterClient(IOptions<LabelPrinterOptions> options, ILogger<LabelPrinterClient> logger) : ILabelPrinterClient
{
    // Like legacy's PrinterDelay pause, this gives the printer time to take the job and answer before the connection closes.
    private const int ReplyIdleTimeoutMilliseconds = 500;
    private const int MaxReplyBytes = 4096;

    private static readonly Action<ILogger, Exception?> LogHostNotConfiguredMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, nameof(LogHostNotConfiguredMessage)),
            "Label printer host is not configured.");

    private static readonly Action<ILogger, string?, int, string, Exception?> LogPrintErrorMessage =
        LoggerMessage.Define<string?, int, string>(
            LogLevel.Error,
            new EventId(2, nameof(LogPrintErrorMessage)),
            "Printer communication failed. Host={Host} Port={Port} Reason={Reason}");

    private static readonly Action<ILogger, string?, int, Exception?> LogConfigurationLoadedMessage =
        LoggerMessage.Define<string?, int>(
            LogLevel.Information,
            new EventId(3, nameof(LogConfigurationLoadedMessage)),
            "Label printer configuration loaded. Host={Host} Port={Port}");

    private static readonly Action<ILogger, string?, int, Exception?> LogAttemptingCommunicationMessage =
        LoggerMessage.Define<string?, int>(
            LogLevel.Information,
            new EventId(4, nameof(LogAttemptingCommunicationMessage)),
            "Attempting printer communication. Host={Host} Port={Port}");

    private static readonly Action<ILogger, string?, int, Exception?> LogConnectionEstablishedMessage =
        LoggerMessage.Define<string?, int>(
            LogLevel.Information,
            new EventId(5, nameof(LogConnectionEstablishedMessage)),
            "Printer connection established. Host={Host} Port={Port}");

    private static readonly Action<ILogger, string?, int, int, Exception?> LogDataSentMessage =
        LoggerMessage.Define<string?, int, int>(
            LogLevel.Information,
            new EventId(6, nameof(LogDataSentMessage)),
            "Label data sent to printer. Host={Host} Port={Port} PayloadLength={PayloadLength}");

    private static readonly Action<ILogger, bool, string?, int, Exception?> LogPrintCompletedMessage =
        LoggerMessage.Define<bool, string?, int>(
            LogLevel.Information,
            new EventId(7, nameof(LogPrintCompletedMessage)),
            "Print request completed. Success={Success} Host={Host} Port={Port}");

    // TEMPORARY: always logged for troubleshooting. Label text is name and address data - revert to Debug or remove.
    private static readonly Action<ILogger, string?, int, int, string, Exception?> LogGeneratedPayloadMessage =
        LoggerMessage.Define<string?, int, int, string>(
            LogLevel.Information,
            new EventId(8, nameof(LogGeneratedPayloadMessage)),
            "Generated Fingerprint payload. Host={Host} Port={Port} PayloadLength={PayloadLength}\n{Payload}");

    private static readonly Action<ILogger, string?, int, int, string, Exception?> LogPrinterReplyMessage =
        LoggerMessage.Define<string?, int, int, string>(
            LogLevel.Information,
            new EventId(9, nameof(LogPrinterReplyMessage)),
            "Printer reply received. Host={Host} Port={Port} ReplyLength={ReplyLength}\n{Reply}");

    private static readonly Action<ILogger, string?, int, int, string, Exception?> LogPrinterReportedErrorMessage =
        LoggerMessage.Define<string?, int, int, string>(
            LogLevel.Warning,
            new EventId(10, nameof(LogPrinterReportedErrorMessage)),
            "Printer reported an error. Host={Host} Port={Port} ReplyLength={ReplyLength}\n{Reply}");

    public async Task<bool> PrintAsync(AddressLabel label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(label);

        var settings = options.Value;
        LogConfigurationLoadedMessage(logger, settings.Host, settings.Port, null);
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            LogHostNotConfiguredMessage(logger, null);
            LogPrintCompletedMessage(logger, false, settings.Host, settings.Port, null);
            return false;
        }

        try
        {
            using var client = new TcpClient();
            using var connectTimeout = new CancellationTokenSource(settings.ConnectTimeoutMilliseconds);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectTimeout.Token);

            LogAttemptingCommunicationMessage(logger, settings.Host, settings.Port, null);
            await client.ConnectAsync(settings.Host, settings.Port, linkedSource.Token);
            LogConnectionEstablishedMessage(logger, settings.Host, settings.Port, null);

            await using var stream = client.GetStream();

            var payload = FingerprintLabelFormatter.ToFingerprint(label);
            LogGeneratedPayloadMessage(logger, settings.Host, settings.Port, payload.Length, Encoding.ASCII.GetString(payload), null);

            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            LogDataSentMessage(logger, settings.Host, settings.Port, payload.Length, null);

            var reply = await ReadReplyAsync(stream, cancellationToken);
            if (reply.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                LogPrinterReportedErrorMessage(logger, settings.Host, settings.Port, reply.Length, reply, null);
            }
            else
            {
                LogPrinterReplyMessage(logger, settings.Host, settings.Port, reply.Length, reply, null);
            }

            LogPrintCompletedMessage(logger, true, settings.Host, settings.Port, null);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            LogPrintErrorMessage(logger, settings.Host, settings.Port, $"{ex.GetType().Name}: {ex.Message}", ex);
            LogPrintCompletedMessage(logger, false, settings.Host, settings.Port, null);
            return false;
        }
    }

    // Collects what the printer sends back (verbose mode) until it has been quiet for a moment. The job has already been
    // sent, so a silent printer or a dropped connection here is not a print failure.
    private static async Task<string> ReadReplyAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var reply = new MemoryStream();
        var buffer = new byte[512];

        while (reply.Length < MaxReplyBytes)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idle.CancelAfter(ReplyIdleTimeoutMilliseconds);

            int read;
            try
            {
                read = await stream.ReadAsync(buffer, idle.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            reply.Write(buffer, 0, read);
        }

        return Encoding.Latin1.GetString(reply.GetBuffer(), 0, (int)reply.Length);
    }
}
