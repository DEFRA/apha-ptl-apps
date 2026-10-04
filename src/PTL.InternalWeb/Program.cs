using PTL.ApiClient;
using PTL.Core.Contract.Document;
using PTL.InternalWeb.Features.Invoice;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();

// Mail merge runs in-process in this app; the template bytes come from S3 via PTL.Api's
// export-template endpoints, so nothing here touches the file system.
builder.Services.AddScoped<ITemplateMergeService, TemplateMergeService>();

// Display-only text for the Invoice Generation page - independent of PTL.Api's own GovUkNotify
// config (see InvoiceNotificationDisplayOptions remarks).
builder.Services.Configure<InvoiceNotificationDisplayOptions>(builder.Configuration.GetSection(InvoiceNotificationDisplayOptions.SectionName));

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.InternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
