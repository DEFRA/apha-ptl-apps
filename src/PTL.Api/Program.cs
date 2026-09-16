using Microsoft.EntityFrameworkCore;
using PTL.Api.Features.Health;
using PTL.Api.Infrastructure;
using PTL.Core.Customer;
using PTL.Data;
using PTL.Data.Customer;

var builder = WebApplication.CreateBuilder(args);

// Database:Host/Name/User/Password are sourced from appsettings.Development.json
// locally; in every deployed environment they come from the
// Database__Host/Database__Name/Database__User/Database__Password environment
// variables, which the ECS task definition injects from Parameter Store at
// container start - never baked into the image or read from appsettings.json.
var databaseOptions = StartupChecks.RequireDatabaseOptions(builder.Configuration);

// HealthCheck__ReadinessKey - same fail-fast reasoning: a broken secret
// wiring here would otherwise be invisible, since ReadinessKeyFilter must
// return an identical 404 for "not configured" and "wrong key" alike.
StartupChecks.RequireReadinessKey(builder.Configuration);

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddControllers();
builder.Services.AddDbContext<PtlDbContext>(options => options.UseSqlServer(databaseOptions.ToConnectionString()));

// TEMPORARY (Development only): see DevelopmentCustomerRepository.cs - falls back to sample data
// when the local dev database has no customer rows. Delete this if-block plus that file to remove.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<CustomerRepository>();
    builder.Services.AddScoped<ICustomerRepository>(sp =>
        new DevelopmentCustomerRepository(sp.GetRequiredService<CustomerRepository>()));
}
else
{
    builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
}

builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PTL.Api.Infrastructure.GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/", () => "Hello World!");

app.MapHealthEndpoints();
app.MapControllers();

app.Run();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.Api.Tests.
public partial class Program { }
