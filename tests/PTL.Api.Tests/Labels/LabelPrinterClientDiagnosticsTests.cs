using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.Tests.Distribution;
using PTL.Core.Labels;

namespace PTL.Api.Tests.Labels;

// The printer client's diagnostics: the logs must show whether configuration loaded, which host was used, whether a
// connection was attempted, whether it succeeded, whether data went out and why a failure happened.
public class LabelPrinterClientDiagnosticsTests
{
    private static readonly AddressLabel Label = new(["Alice Example", "1 Sample Street"]);

    private static LabelPrinterClient CreateClient(string host, int port, MessageCapturingLogger<LabelPrinterClient> logger) =>
        new(Options.Create(new LabelPrinterOptions { Host = host, Port = port, ConnectTimeoutMilliseconds = 2000 }), logger);

    private static string Value(MessageCapturingLogger<LabelPrinterClient>.Entry entry, string name) =>
        Convert.ToString(entry.State.Single(kv => kv.Key == name).Value, CultureInfo.InvariantCulture)!;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PrintAsync_BlankHost_LogsConfigurationAndThatTheHostIsNotConfigured_WithoutConnecting(string host)
    {
        var logger = new MessageCapturingLogger<LabelPrinterClient>();

        var printed = await CreateClient(host, 9100, logger).PrintAsync(Label);

        Assert.False(printed);
        Assert.Equal([3, 1, 7], logger.Entries.Select(e => e.EventId.Id));
        Assert.Equal("Label printer host is not configured.", logger.Entries[1].Message);
        Assert.Equal("False", Value(logger.Entries[2], "Success"));
    }

    [Fact]
    public async Task PrintAsync_Success_LogsEachStageWithHostPortAndPayloadLength()
    {
        var logger = new MessageCapturingLogger<LabelPrinterClient>();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var printed = await CreateClient("127.0.0.1", port, logger).PrintAsync(Label);

        using var accepted = await listener.AcceptTcpClientAsync();
        using var received = new MemoryStream();
        await accepted.GetStream().CopyToAsync(received);

        var expected = ZplLabelFormatter.ToZpl(Label);
        Assert.True(printed);
        Assert.Equal(expected, received.ToArray());
        Assert.Equal([3, 4, 5, 6, 7], logger.Entries.Select(e => e.EventId.Id));
        Assert.All(logger.Entries, e =>
        {
            Assert.Equal(LogLevel.Information, e.Level);
            Assert.Equal("127.0.0.1", Value(e, "Host"));
            Assert.Equal(port.ToString(CultureInfo.InvariantCulture), Value(e, "Port"));
        });
        Assert.Equal(expected.Length.ToString(CultureInfo.InvariantCulture), Value(logger.Entries[3], "PayloadLength"));
        Assert.Equal("True", Value(logger.Entries[4], "Success"));
        logger.AssertNoneContain("Alice", "Sample Street");
    }

    [Fact]
    public async Task PrintAsync_ConnectionRefused_LogsTheAttemptTheReasonAndTheException()
    {
        var logger = new MessageCapturingLogger<LabelPrinterClient>();
        int port;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        var printed = await CreateClient("127.0.0.1", port, logger).PrintAsync(Label);

        Assert.False(printed);
        Assert.Equal([3, 4, 2, 7], logger.Entries.Select(e => e.EventId.Id));
        var failure = logger.Entries[2];
        Assert.Equal(LogLevel.Error, failure.Level);
        // Windows retries a refused connection until the connect timeout, so the cause may be a cancellation.
        Assert.True(failure.Exception is SocketException or OperationCanceledException);
        Assert.StartsWith(failure.Exception!.GetType().Name + ":", Value(failure, "Reason"), StringComparison.Ordinal);
        Assert.Equal("False", Value(logger.Entries[3], "Success"));
    }
}
