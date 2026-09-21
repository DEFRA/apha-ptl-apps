using PTL.ApiClient;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();

var app = builder.Build();
app.UsePtlWebFrontEnd();

app.Run();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.ExternalWeb.Tests.
public partial class Program { }
