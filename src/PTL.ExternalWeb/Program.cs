using PTL.ApiClient;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.ExternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
