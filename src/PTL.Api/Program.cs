using System.Reflection;
using Microsoft.OpenApi;
using PTL.Api.Features.Health;
using PTL.Api.Infrastructure;
using PTL.Common.Correlation;
using PTL.Common.Health;
using PTL.Core.Contract;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Customer;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using PTL.Data.Contract;
using PTL.Data.Contract.ImportPermit;
using PTL.Data.Customer;
using PTL.Data.Infrastructure;
using PTL.Data.Lookup;
using PTL.Data.Participant;
using PTL.Data.Scheme;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Maps stored-procedure result columns (fldXxx, plus a few aliases like "Readonly") onto entity
// properties for every repository's Dapper queries - must run before any repository is used.
DapperColumnMappings.Register();

// Structured JSON to stdout only - ECS/Fargate storage is ephemeral, so no file sinks. The
// awslogs driver on the container picks stdout/stderr up and ships it to CloudWatch Logs.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// Database:Host/Name/User/Password are sourced from appsettings.Development.json
// locally; in every deployed environment they come from the
// Database__Host/Database__Name/Database__User/Database__Password environment
// variables, which the ECS task definition injects from Parameter Store at
// container start - never baked into the image or read from appsettings.json.
StartupChecks.RequireDatabaseOptions(builder.Configuration);

// HealthCheck__ReadinessKey - same fail-fast reasoning: a broken secret
// wiring here would otherwise be invisible, since ReadinessKeyFilter must
// return an identical 404 for "not configured" and "wrong key" alike.
StartupChecks.RequireReadinessKey(builder.Configuration);

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddControllers();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddScoped<IParticipantSchemeRepository, ParticipantSchemeRepository>();
builder.Services.AddScoped<IParticipantSchemeService, ParticipantSchemeService>();

builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IContractService, ContractService>();

builder.Services.AddScoped<IImportPermitRepository, ImportPermitRepository>();
builder.Services.AddScoped<IImportPermitService, ImportPermitService>();

builder.Services.AddScoped<ISchemeRepository, SchemeRepository>();
builder.Services.AddScoped<ISchemeService, SchemeService>();

builder.Services.AddScoped<ILookupRepository, LookupRepository>();
builder.Services.AddScoped<ILookupService, LookupService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PTL.Api.Infrastructure.GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PTLIMS API",
        Version = "v1",
        Description = "API for the Proficiency Testing Laboratory Information Management System (PTLIMS) - Customer, Participant, Contract, and reference data lookups."
    });

    // Actions are tagged by controller name by default (e.g. Customer, Contract, Participant,
    // Lookup), which is what groups the endpoints by domain in the Swagger UI.
    options.TagActionsBy(api => [api.ActionDescriptor.RouteValues["controller"] ?? "Default"]);
    options.DocInclusionPredicate((_, _) => true);

    // Picks up <summary>/<param>/<returns> comments from controllers and request/response DTOs.
    var apiXmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(apiXmlFile))
    {
        options.IncludeXmlComments(apiXmlFile, includeControllerXmlComments: true);
    }

    var contractsXmlFile = Path.Combine(AppContext.BaseDirectory, "PTL.Contracts.xml");
    if (File.Exists(contractsXmlFile))
    {
        options.IncludeXmlComments(contractsXmlFile);
    }

    const string bearerScheme = "Bearer";
    options.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT bearer token. Example: \"Bearer {token}\""
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(bearerScheme, document)] = []
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PTLIMS API v1");
        options.DocumentTitle = "PTLIMS API Documentation";
    });
}

// Correlation ID before request logging so the one-line-per-request log carries it; the web
// front-ends forward their own ID here via CorrelationIdDelegatingHandler, so all services' logs
// correlate.
app.UseCorrelationId();
app.UseSerilogRequestLogging();

app.MapGet("/", () => "Hello World!");

app.MapHealthEndpoints();
app.MapControllers();

await app.RunAsync();
