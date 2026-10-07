using PTL.ApiClient;
using PTL.Auth.Entra;
using PTL.Auth.Entra.Events;
using PTL.Core.Contract.Document;
using PTL.InternalWeb.Features.Account;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddEntraAuthentication();
builder.Services.AddScoped<IEntraInternalUserResolver, InternalUserResolver>();

// Mail merge runs in-process in this app; the template bytes come from S3 via PTL.Api's
// export-template endpoints, so nothing here touches the file system.
builder.Services.AddScoped<ITemplateMergeService, TemplateMergeService>();

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.InternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
