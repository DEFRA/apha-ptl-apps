using System.Reflection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using PTL.Api.Features.Health;
using PTL.Api.Infrastructure;
using PTL.Common.Correlation;
using PTL.Common.Health;
using PTL.Core.AdministrationCharge;
using PTL.Core.Contract;
using PTL.Core.Contract.Export.Bulk;
using PTL.Core.Contract.Export.Templates;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Contract.Renew;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;
using PTL.Core.Customer;
using PTL.Core.GroupAddress;
using PTL.Core.Invoice;
using PTL.Core.Lookup;
using PTL.Core.Notifications;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using PTL.Core.Viewer;
using PTL.Core.WeightedPricingPlan;
using PTL.Data.AdministrationCharge;
using PTL.Data.Contract;
using PTL.Data.Contract.Export;
using PTL.Data.Contract.ImportPermit;
using PTL.Data.Contract.PendingOrder;
using PTL.Data.Contract.Renew;
using PTL.Data.Contract.Renewal;
using PTL.Data.Contract.SampleAddress;
using PTL.Data.Customer;
using PTL.Data.GroupAddress;
using PTL.Data.Infrastructure;
using PTL.Data.Invoice;
using PTL.Data.Lookup;
using PTL.Data.Notifications;
using PTL.Data.Participant;
using PTL.Data.Scheme;
using PTL.Data.Storage;
using PTL.Data.Viewer;
using PTL.Data.WeightedPricingPlan;
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
builder.Services.AddScoped<IPendingCustomerUpdateRepository, PendingCustomerUpdateRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddScoped<IGroupAddressRepository, GroupAddressRepository>();
builder.Services.AddScoped<IGroupAddressService, GroupAddressService>();

builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();
builder.Services.AddScoped<IPendingParticipantUpdateRepository, PendingParticipantUpdateRepository>();
builder.Services.AddScoped<IParticipantViewerRepository, ParticipantViewerRepository>();
builder.Services.AddScoped<IViewerRepository, ViewerRepository>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddScoped<IParticipantSchemeRepository, ParticipantSchemeRepository>();
builder.Services.AddScoped<IParticipantSchemeService, ParticipantSchemeService>();

builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IContractService, ContractService>();

builder.Services.AddScoped<IImportPermitRepository, ImportPermitRepository>();
builder.Services.AddScoped<IImportPermitService, ImportPermitService>();

builder.Services.AddScoped<ISampleAddressRepository, SampleAddressRepository>();
builder.Services.AddScoped<ISampleAddressService, SampleAddressService>();

builder.Services.AddScoped<IContractRenewalRepository, ContractRenewalRepository>();
builder.Services.AddScoped<IContractRenewalService, ContractRenewalService>();

builder.Services.AddScoped<IContractMergeRepository, ContractMergeRepository>();
builder.Services.AddScoped<IRenewContractsService, RenewContractsService>();
builder.Services.AddScoped<IPendingOrderRepository, PendingOrderRepository>();
builder.Services.AddScoped<IPendingOrderService, PendingOrderService>();

builder.Services.Configure<TemplateStorageOptions>(builder.Configuration.GetSection(TemplateStorageOptions.SectionName));
builder.Services.AddScoped<IUploadedTemplateRepository, UploadedTemplateRepository>();
builder.Services.AddScoped<IExportTemplateService, ExportTemplateService>();
builder.Services.AddScoped<IBulkExportRepository, BulkExportRepository>();
builder.Services.AddScoped<IBulkExportService, BulkExportService>();

// The S3 client is only resolved when a template/invoice CSV is actually read or written, so the
// application starts and every test runs without AWS credentials or bucket access. Registered once,
// unconditionally, since either TemplateStorage or InvoiceStorage alone may select the S3 provider.
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();

if (string.Equals(builder.Configuration[$"{TemplateStorageOptions.SectionName}:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ITemplateStorageService, InMemoryTemplateStorageService>();
}
else
{
    builder.Services.AddScoped<ITemplateStorageService, S3TemplateStorageService>();
}

builder.Services.AddScoped<ISchemeRepository, SchemeRepository>();
builder.Services.AddScoped<ISchemeService, SchemeService>();

builder.Services.AddScoped<ILookupRepository, LookupRepository>();
builder.Services.AddScoped<ILookupService, LookupService>();

builder.Services.AddScoped<IAdministrationChargeRepository, AdministrationChargeRepository>();
builder.Services.AddScoped<IAdministrationChargeService, AdministrationChargeService>();

builder.Services.AddScoped<IWeightedPricingPlanRepository, WeightedPricingPlanRepository>();
builder.Services.AddScoped<IWeightedPricingPlanService, WeightedPricingPlanService>();

builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

builder.Services.Configure<InvoiceStorageOptions>(builder.Configuration.GetSection(InvoiceStorageOptions.SectionName));
if (string.Equals(builder.Configuration[$"{InvoiceStorageOptions.SectionName}:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IInvoiceStorageService, InMemoryInvoiceStorageService>();
}
else
{
    builder.Services.AddScoped<IInvoiceStorageService, S3InvoiceStorageService>();
}

// Shared GOV.UK Notify client - every feature needing outbound notifications depends on
// INotifyClient only, never SMTP/EmailHelper (docs/migration/email-notification-migration.md).
builder.Services.Configure<NotifyOptions>(builder.Configuration.GetSection(NotifyOptions.SectionName));
builder.Services.Configure<InvoiceNotificationOptions>(builder.Configuration.GetSection(InvoiceNotificationOptions.SectionName));
builder.Services.AddHttpClient<INotifyClient, NotifyClient>((services, client) =>
{
    // BaseUrl is always sourced from appsettings.json/environment (GovUkNotify:BaseUrl) - no
    // in-code fallback, so the endpoint can be changed per-environment without a code change.
    var notifyOptions = services.GetRequiredService<IOptions<NotifyOptions>>().Value;
    client.BaseAddress = new Uri(notifyOptions.BaseUrl);
});

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
