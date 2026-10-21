using System.Reflection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using PTL.Api.Features.Health;
using PTL.Api.Infrastructure;
using PTL.Common.Correlation;
using PTL.Common.Health;
using PTL.Core.AdministrationCharge;
using PTL.Core.Contract;
using PTL.Core.Contract.Document;
using PTL.Core.Contract.Export.Bulk;
using PTL.Core.Contract.Export.Templates;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Contract.Renew;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;
using PTL.Core.Country;
using PTL.Core.Customer;
using PTL.Core.Distribution;
using PTL.Core.ExternalSiteMessage;
using PTL.Core.ExternalUser;
using PTL.Core.GroupAddress;
using PTL.Core.InternalUser;
using PTL.Core.Invoice;
using PTL.Core.Lookup;
using PTL.Core.Notifications;
using PTL.Core.Participant;
using PTL.Core.PostagePricingPlan;
using PTL.Core.Scheme;
using PTL.Core.SystemMessage;
using PTL.Core.TestConsultant;
using PTL.Core.User;
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
using PTL.Data.Country;
using PTL.Data.Customer;
using PTL.Data.Distribution;
using PTL.Data.ExternalSiteMessage;
using PTL.Data.GroupAddress;
using PTL.Data.Infrastructure;
using PTL.Data.InternalUser;
using PTL.Data.Invoice;
using PTL.Data.Lookup;
using PTL.Data.Notifications;
using PTL.Data.Participant;
using PTL.Data.PostagePricingPlan;
using PTL.Data.Scheme;
using PTL.Data.Storage;
using PTL.Data.SystemMessage;
using PTL.Data.TestConsultant;
using PTL.Data.User;
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PTL.Core.Audit.IActingUserAccessor, HttpActingUserAccessor>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IPendingCustomerUpdateRepository, PendingCustomerUpdateRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddScoped<PTL.Core.Search.ISearchRepository, PTL.Data.Search.SearchRepository>();
builder.Services.AddScoped<PTL.Core.Search.ISearchService, PTL.Core.Search.SearchService>();

builder.Services.AddScoped<IGroupAddressRepository, GroupAddressRepository>();
builder.Services.AddScoped<IGroupAddressService, GroupAddressService>();

builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();
builder.Services.AddScoped<IPendingParticipantUpdateRepository, PendingParticipantUpdateRepository>();
builder.Services.AddScoped<IParticipantViewerRepository, ParticipantViewerRepository>();
builder.Services.AddScoped<IViewerRepository, ViewerRepository>();
builder.Services.AddScoped<IViewerService, ViewerService>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddScoped<IParticipantSchemeRepository, ParticipantSchemeRepository>();
builder.Services.AddScoped<IParticipantSchemeService, ParticipantSchemeService>();

builder.Services.AddScoped<ITestConsultantRepository, TestConsultantRepository>();
builder.Services.AddScoped<IExternalUserService, ExternalUserService>();
builder.Services.AddScoped<IExternalLoginService, StubExternalLoginService>();
builder.Services.AddScoped<ITestConsultantService, TestConsultantService>();

builder.Services.AddScoped<ISystemMessageRepository, SystemMessageRepository>();
builder.Services.AddScoped<ISystemMessageService, SystemMessageService>();

builder.Services.AddScoped<IInternalUserRepository, InternalUserRepository>();
builder.Services.AddScoped<IInternalUserService, InternalUserService>();

builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IContractPostageService, ContractPostageService>();
builder.Services.AddScoped<PTL.Core.Audit.IAuditActorResolver, PTL.Core.Audit.AuditActorResolver>();

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
builder.Services.AddScoped<IPendingOrderContractWriter, PendingOrderContractWriter>();
builder.Services.AddScoped<IPendingOrderDecisionEmails, PendingOrderDecisionEmails>();
builder.Services.AddScoped<IPendingOrderEmailPersonaliser, PendingOrderEmailPersonaliser>();

// Approve/Decline emails: sent through GOV.UK Notify straight after the decision, retried by the hosted scheduler.
builder.Services.Configure<OnlineOrderEmailOptions>(builder.Configuration.GetSection(OnlineOrderEmailOptions.SectionName));
builder.Services.AddSingleton<EmailSendGuard>();
builder.Services.AddScoped<IPendingOrderNotifier, PendingOrderNotifier>();

// Customer and participant update emails: same pattern - sent through GOV.UK Notify when the update is decided, retried by the scheduler.
builder.Services.Configure<CustomerUpdateEmailOptions>(builder.Configuration.GetSection(CustomerUpdateEmailOptions.SectionName));
builder.Services.Configure<ParticipantUpdateEmailOptions>(builder.Configuration.GetSection(ParticipantUpdateEmailOptions.SectionName));
builder.Services.AddScoped<ICustomerUpdateNotifier, CustomerUpdateNotifier>();
builder.Services.AddScoped<IParticipantUpdateNotifier, ParticipantUpdateNotifier>();

// Single shared S3 configuration root for every S3-backed feature (export templates, invoice
// archives) - one bucket, one prefix per feature (docs/migration/invoice-migration.md). Provider
// and region never vary by environment, so they are not bound from configuration at all - see
// PTL.Core.Storage.S3Options.DefaultRegion and the AddSingleton<IAmazonS3> registration below.
builder.Services.Configure<PTL.Core.Storage.S3Options>(builder.Configuration.GetSection(PTL.Core.Storage.S3Options.SectionName));

builder.Services.AddOptions<TemplateStorageOptions>()
    .Configure<IOptions<PTL.Core.Storage.S3Options>>((options, s3) =>
    {
        options.BucketName = s3.Value.Bucket;
        options.Prefix = s3.Value.Templates.Prefix;
        options.MaxUploadBytes = s3.Value.Templates.MaxUploadBytes;
    });
builder.Services.AddScoped<IUploadedTemplateRepository, UploadedTemplateRepository>();
builder.Services.AddScoped<IExportTemplateService, ExportTemplateService>();
builder.Services.AddScoped<IBulkExportRepository, BulkExportRepository>();
builder.Services.AddScoped<IBulkExportService, BulkExportService>();

// Use the AWS SDK default credential chain rather than hardcoded keys or appsettings credentials.
// Local development resolves the authenticated IAM Identity Center profile; deployed environments
// resolve the task/instance role automatically from the runtime environment.
builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(_ => AwsS3ClientFactory.Create(PTL.Core.Storage.S3Options.DefaultRegion));

// "InMemory" is a local-developer-only convenience (set via an untracked appsettings.Development.
// json) for running the feature before a bucket is provisioned - never present in any deployed
// environment's configuration, so it is deliberately not a bound S3Options property.
if (string.Equals(builder.Configuration[$"{PTL.Core.Storage.S3Options.SectionName}:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
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

builder.Services.AddScoped<IDistributionRepository, DistributionRepository>();
builder.Services.AddScoped<IDistributionService, DistributionService>();
builder.Services.AddScoped<ISampleNumbersService, SampleNumbersService>();
builder.Services.AddScoped<IIntendedResultsService, IntendedResultsService>();
builder.Services.AddScoped<IDistributionParticipantsService, DistributionParticipantsService>();
builder.Services.AddScoped<IPreparationService, PreparationService>();
builder.Services.AddScoped<IResultsReceiptService, ResultsReceiptService>();
builder.Services.AddScoped<ITabulationsService, TabulationsService>();
builder.Services.AddScoped<ITabulationReportService, TabulationReportService>();
builder.Services.AddScoped<ITestConsultantCommentsService, TestConsultantCommentsService>();
builder.Services.AddScoped<IAmendResultsService, AmendResultsService>();
builder.Services.AddScoped<ITabulationSendService, TabulationSendService>();
builder.Services.AddScoped<IActualResultsService, ActualResultsService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDistributionPostedNotifier, DistributionPostedNotifier>();

// Test Consultant and publication emails: sent through GOV.UK Notify; the one scheduler for all email jobs is hosted in this process.
builder.Services.Configure<NotificationSchedulerOptions>(builder.Configuration.GetSection(NotificationSchedulerOptions.SectionName));
builder.Services.Configure<TabulationEmailOptions>(builder.Configuration.GetSection(TabulationEmailOptions.SectionName));
builder.Services.Configure<PublicationNotificationOptions>(builder.Configuration.GetSection(PublicationNotificationOptions.SectionName));
builder.Services.AddSingleton<TabulationEmailGuard>();
builder.Services.AddSingleton<TabulationEmailContext>();
builder.Services.AddScoped<ITabulationEmailService, TabulationEmailService>();
builder.Services.AddScoped<ITabulationPublishedNotifier, TabulationPublishedNotifier>();
builder.Services.AddHostedService<PTL.Api.BackgroundServices.NotificationBackgroundService>();
builder.Services.AddScoped<IPackagingService, PackagingService>();
builder.Services.AddScoped<IPackagingOutputService, PackagingOutputService>();
builder.Services.AddScoped<ITemplateMergeService, TemplateMergeService>();
builder.Services.Configure<PTL.Core.Configuration.ExternalOptions>(builder.Configuration.GetSection(PTL.Core.Configuration.ExternalOptions.SectionName));

builder.Services.AddScoped<IAdministrationChargeRepository, AdministrationChargeRepository>();
builder.Services.AddScoped<IAdministrationChargeService, AdministrationChargeService>();

builder.Services.AddScoped<IWeightedPricingPlanRepository, WeightedPricingPlanRepository>();
builder.Services.AddScoped<IWeightedPricingPlanService, WeightedPricingPlanService>();

builder.Services.AddScoped<IPostagePricingPlanRepository, PostagePricingPlanRepository>();
builder.Services.AddScoped<IPostagePricingPlanService, PostagePricingPlanService>();

builder.Services.AddScoped<ICountryRepository, CountryRepository>();
builder.Services.AddScoped<ICountryService, CountryService>();

builder.Services.AddScoped<IExternalSiteMessageRepository, ExternalSiteMessageRepository>();
builder.Services.AddScoped<IExternalSiteMessageService, ExternalSiteMessageService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IStaffDirectoryService, StubStaffDirectoryService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IUserRoleRepository, UserRoleRepository>();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();

builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

builder.Services.AddOptions<InvoiceStorageOptions>()
    .Configure<IOptions<PTL.Core.Storage.S3Options>>((options, s3) =>
    {
        options.BucketName = s3.Value.Bucket;
        options.Prefix = s3.Value.Invoices.Prefix;
    });
if (string.Equals(builder.Configuration[$"{PTL.Core.Storage.S3Options.SectionName}:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IInvoiceStorageService, InMemoryInvoiceStorageService>();
}
else
{
    builder.Services.AddScoped<IInvoiceStorageService, S3InvoiceStorageService>();
}

// Distribution attachments are stored only in the database (fldFileData), as in legacy.
builder.Services.AddSingleton<PTL.Core.Distribution.IAttachmentStagingStore, InMemoryAttachmentStagingStore>();

// Shared GOV.UK Notify client - every feature needing outbound notifications depends on
// INotifyClient only, never SMTP/EmailHelper (docs/migration/email-notification-migration.md).
builder.Services.Configure<NotifyOptions>(builder.Configuration.GetSection(NotifyOptions.SectionName));
builder.Services.Configure<InvoiceNotificationOptions>(builder.Configuration.GetSection(InvoiceNotificationOptions.SectionName));
builder.Services.Configure<PTL.Core.Configuration.InternalOptions>(builder.Configuration.GetSection(PTL.Core.Configuration.InternalOptions.SectionName));
builder.Services.AddHttpClient<INotifyClient, NotifyClient>((services, client) =>
{
    // BaseUrl is always sourced from appsettings.json/environment (Notification:BaseUrl) - no
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

// TEMPORARY DIAGNOSTIC - remove once SSO credential resolution is confirmed fixed in every
// environment. Logs only the resolved credential provider type and a masked key prefix - never
// the secret key or session token - to prove which link of the default credential chain resolved.
var logCredentialDiagnostic = LoggerMessage.Define<string, string, bool>(
    LogLevel.Information,
    new EventId(1, "AwsCredentialDiagnostic"),
    "AWS credential resolution diagnostic: ProviderType={ProviderType} AccessKeyPrefix={AccessKeyPrefix} UsesSessionToken={UsesSessionToken}");

var logCredentialDiagnosticFailed = LoggerMessage.Define(
    LogLevel.Warning,
    new EventId(2, "AwsCredentialDiagnosticFailed"),
    "AWS credential resolution diagnostic failed at startup.");

try
{
    var s3Client = app.Services.GetRequiredService<Amazon.S3.IAmazonS3>();
#pragma warning disable CS0618 // FallbackCredentialsFactory is obsolete but still the simplest way to surface which provider resolved
    var resolvedCredentials = Amazon.Runtime.FallbackCredentialsFactory.GetCredentials(s3Client.Config);
#pragma warning restore CS0618
    var immutableCredentials = await resolvedCredentials.GetCredentialsAsync();
    var maskedAccessKey = immutableCredentials.AccessKey is { Length: > 4 } key ? $"{key[..4]}***" : "unknown";
    logCredentialDiagnostic(
        app.Logger,
        resolvedCredentials.GetType().Name,
        maskedAccessKey,
        !string.IsNullOrEmpty(immutableCredentials.Token),
        null);
}
catch (Exception ex)
{
    logCredentialDiagnosticFailed(app.Logger, ex);
}

// TEMPORARY DIAGNOSTIC - logs the full Notify API key at the user's explicit request; remove and rotate the key afterwards.
var logNotifyKeyDiagnostic = LoggerMessage.Define<bool, int, string>(
    LogLevel.Information,
    new EventId(3, "NotifyApiKeyDiagnostic"),
    "GOV.UK Notify API key diagnostic: Configured={Configured} Length={Length} Key={ApiKey}");

var configuredNotifyKey = app.Services.GetRequiredService<IOptions<NotifyOptions>>().Value.ApiKey;
logNotifyKeyDiagnostic(
    app.Logger,
    !string.IsNullOrWhiteSpace(configuredNotifyKey),
    configuredNotifyKey.Length,
    configuredNotifyKey,
    null);

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
