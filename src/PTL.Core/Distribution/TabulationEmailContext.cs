using System.Globalization;
using Microsoft.Extensions.Options;
using PTL.Core.Configuration;

namespace PTL.Core.Distribution;

// The calendar day and the links the Tabulation emails use, all taken from configuration.
public sealed class TabulationEmailContext(
    TimeProvider timeProvider,
    IOptions<TabulationEmailOptions> options,
    IOptions<InternalOptions> internalOptions,
    IOptions<ExternalOptions> externalOptions)
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    // Today's date in the configured time zone.
    public DateTime Today() => TimeZoneInfo.ConvertTime(UtcNow, TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone)).Date;

    public static string Format(DateTime day) => day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // Legacy <<WebAddress>>: external consultants use CommentsEntry, internal ones Test Consultant/Tabulation.
    public string CommentsLink(Guid monthlyDistributionSchemeId, bool isExternal) => isExternal
        ? $"{externalOptions.Value.AppUrl.TrimEnd('/')}/{options.Value.ExternalCommentsPath.Trim('/')}?ref={monthlyDistributionSchemeId}"
        : $"{internalOptions.Value.AppUrl.TrimEnd('/')}/{options.Value.InternalCommentsPath.Trim('/')}?MonthlyDistributionSchemeId={monthlyDistributionSchemeId}";

    // Legacy <<ExternalServer>>/<<ExternalAddress>> were two settings; here both come from External:AppUrl.
    public (string Server, string Address) ExternalLocation()
    {
        var appUrl = externalOptions.Value.AppUrl;
        return Uri.TryCreate(appUrl, UriKind.Absolute, out var uri)
            ? (uri.GetLeftPart(UriPartial.Authority), uri.AbsolutePath.Trim('/'))
            : (appUrl.TrimEnd('/'), string.Empty);
    }
}
