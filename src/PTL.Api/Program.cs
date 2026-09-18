using Microsoft.EntityFrameworkCore;
using PTL.Api.Features.Health;
using PTL.Api.Infrastructure;
using PTL.Core.Customer;
using PTL.Core.Participant;
using PTL.Core.Contract;
using PTL.Core.Lookup;
using PTL.Data;
using PTL.Data.Customer;
using PTL.Data.Participant;
using PTL.Data.Contract;
using PTL.Data.Lookup;

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

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();

builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IContractService, ContractService>();

builder.Services.AddScoped<ILookupRepository, LookupRepository>();
builder.Services.AddScoped<ILookupService, LookupService>();

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
