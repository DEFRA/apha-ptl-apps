namespace PTL.Core.Labels;

// Network equivalent of legacy's web.config LabelPrinterName - the printer is now reached over TCP
// rather than resolved as a Windows printer queue on the host machine.
public sealed class LabelPrinterOptions
{
    public const string SectionName = "LabelPrinter";

    // Hostname or IP address of the label printer. Left blank until configured per environment -
    // LabelPrinterClient treats a blank Host as "not configured" and fails the print cleanly rather
    // than attempting a connection, matching legacy's own "Print() returns False" failure mode for a
    // misconfigured LabelPrinterName.
    public string Host { get; set; } = string.Empty;

    // 9100 is the de-facto standard raw/"JetDirect" print port most network label printers (including
    // the Intermec PC43t legacy pointed at) accept without needing a Windows printer driver.
    public int Port { get; set; } = 9100;

    public int ConnectTimeoutMilliseconds { get; set; } = 5000;
}
