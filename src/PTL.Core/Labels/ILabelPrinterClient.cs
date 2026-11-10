namespace PTL.Core.Labels;

// Replaces legacy LabelBase.Print() (System.Drawing.Printing.PrintDocument -> the web server's own
// Windows print spooler, see AddressLabelPdfRenderer's previous header comment for why that couldn't
// be ported directly). The printer is assumed reachable on the same network as this application and
// is addressed directly over a raw TCP socket instead of through an OS print driver - legacy never
// sent a file to the browser, and neither does this.
public interface ILabelPrinterClient
{
    Task<bool> PrintAsync(AddressLabel label, CancellationToken cancellationToken = default);
}
