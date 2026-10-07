using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PTL.Common.Health;

namespace PTL.Api.Tests.Common;

public class HealthCheckResponseWriterTests
{
    [Fact]
    public async Task WriteResponse_WithEntryException_IncludesExceptionMessage()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new HealthReportEntry(
                HealthStatus.Unhealthy, "db unreachable", TimeSpan.FromMilliseconds(5),
                new InvalidOperationException("boom"), data: null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(5));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckResponseWriter.WriteResponse(context, report);

        var body = ReadBody(context);
        Assert.Contains("boom", body);
        Assert.Contains("Unhealthy", body);
    }

    [Fact]
    public async Task WriteResponse_WithoutEntryException_OmitsExceptionDetail()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new HealthReportEntry(
                HealthStatus.Healthy, "ok", TimeSpan.FromMilliseconds(1), exception: null, data: null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(1));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckResponseWriter.WriteResponse(context, report);

        var body = ReadBody(context);
        Assert.Contains("\"exception\":null", body);
        Assert.Equal("application/json", context.Response.ContentType);
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return reader.ReadToEnd();
    }
}
