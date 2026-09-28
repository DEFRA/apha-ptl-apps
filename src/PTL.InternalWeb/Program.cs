using PTL.ApiClient;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddPtlDefaultCookieAuthentication();

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.InternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
