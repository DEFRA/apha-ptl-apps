using PTL.ApiClient;
using PTL.Auth.Cidm;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddCidmAuthentication();

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.ExternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
