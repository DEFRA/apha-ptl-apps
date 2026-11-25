using System.Net;
using PTL.Core.Scheme;

namespace PTL.Core.SystemMessage;

// The external site renders these messages as HTML. Rows saved before the save-time decode fix hold entity-escaped
// HTML, and legacy stored whatever was posted, so each is decoded (the repair ExternalWeb used to apply itself) and
// then restricted to the rich text allow-list here, on read - decoding after sanitising would undo it (ITHC-001).
public sealed class SystemMessageService(ISystemMessageRepository systemMessageRepository) : ISystemMessageService
{
    public async Task<string?> GetImportantMessageAsync(CancellationToken cancellationToken = default) =>
        ToSafeHtml(await systemMessageRepository.GetImportantMessageAsync(cancellationToken));

    public async Task<string?> GetMessageAsync(CancellationToken cancellationToken = default) =>
        ToSafeHtml(await systemMessageRepository.GetMessageAsync(cancellationToken));

    private static string? ToSafeHtml(string? stored) =>
        stored is null ? null : SchemeInstructions.Sanitise(WebUtility.HtmlDecode(stored));
}
