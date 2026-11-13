using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PTL.Core.Labels;

namespace PTL.Api.Tests.Labels;

public class LabelPrinterClientTests
{
    private static LabelPrinterClient CreateClient(LabelPrinterOptions options) =>
        new(Options.Create(options), NullLogger<LabelPrinterClient>.Instance);

    [Fact]
    public async Task PrintAsync_ConfiguredHost_SendsTheFingerprintFormattedLabelOverTheSocket()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        var client = CreateClient(new LabelPrinterOptions { Host = "127.0.0.1", Port = port });
        var label = AddressLabel.FromFields("Alice Example", "1 Sample Street", "United Kingdom");

        var printTask = client.PrintAsync(label);

        using var serverClient = await acceptTask;
        using var serverStream = serverClient.GetStream();
        using var buffer = new MemoryStream();
        var readBuffer = new byte[256];
        int read;
        // The write side closes the connection once flushed, so read-to-completion is reliable here.
        while ((read = await serverStream.ReadAsync(readBuffer)) > 0)
        {
            buffer.Write(readBuffer, 0, read);
        }

        Assert.True(await printTask);
        Assert.Equal(FingerprintLabelFormatter.ToFingerprint(label), buffer.ToArray());
    }

    [Fact]
    public async Task PrintAsync_BlankHost_ReturnsFalseWithoutAttemptingAConnection()
    {
        var client = CreateClient(new LabelPrinterOptions { Host = string.Empty });

        var result = await client.PrintAsync(AddressLabel.FromFields("Alice Example"));

        Assert.False(result);
    }

    [Fact]
    public async Task PrintAsync_UnreachableHost_ReturnsFalseRatherThanThrowing()
    {
        // Port 1 on loopback is reserved/unlisted - connection is refused almost immediately.
        var client = CreateClient(new LabelPrinterOptions { Host = "127.0.0.1", Port = 1, ConnectTimeoutMilliseconds = 2000 });

        var result = await client.PrintAsync(AddressLabel.FromFields("Alice Example"));

        Assert.False(result);
    }

    [Fact]
    public async Task PrintAsync_NullLabel_Throws()
    {
        var client = CreateClient(new LabelPrinterOptions { Host = "127.0.0.1" });

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.PrintAsync(null!));
    }
}
