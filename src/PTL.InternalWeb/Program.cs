using PTL.ApiClient;
using PTL.Core.Contract.Document;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();

// Contract document generation is internal-only and reads templates shipped with this app, so it is
// consumed directly by ContractController rather than through PTL.Api.
builder.Services.AddSingleton<ITemplateLoader>(_ => new FileTemplateLoader(builder.Configuration["Documents:TemplatesRoot"]));
builder.Services.AddScoped<ITemplateRepository, FileTemplateRepository>();
builder.Services.AddScoped<ITemplateMergeService, TemplateMergeService>();
builder.Services.AddScoped<IContractDocumentService, ContractDocumentService>();

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.InternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
