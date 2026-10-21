using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace PTL.InternalWeb.Authorization;

// One policy per legacy Web.config <location> folder, plus the finer legacy IsInRole checks. Legacy
// Admin is not a superuser - each policy needs its own role.
public static class PtlPolicies
{
    public const string Admin = "Admin";
    public const string ContractsAdmin = "ContractsAdmin";
    public const string SchemeAdmin = "SchemeAdmin";
    public const string InternalUser = "InternalUser";
    public const string Scheduling = "Scheduling";
    public const string Distributions = "Distributions";
    public const string TestConsultant = "TestConsultant";
    public const string Assessor = "Assessor";
    public const string ResultsSignOff = "ResultsSignOff";

    // Legacy: Invoice Generation lives under Contracts Admin AND needs the Generate Invoices role.
    public const string InvoiceGeneration = "InvoiceGeneration";
    public const string OverrideSchemes = "OverrideSchemes";
    public const string Support = "Support";

    /// <summary>
    /// Registers the role claims transformation, the policies above and a fallback policy that makes every
    /// endpoint require a signed-in user unless it opts out with [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AddPtlAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IClaimsTransformation, PtlRoleClaimsTransformation>();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Admin, policy => policy.RequireRole(PtlRoles.Admin))
            .AddPolicy(ContractsAdmin, policy => policy.RequireRole(PtlRoles.ContractsAdmin))
            .AddPolicy(SchemeAdmin, policy => policy.RequireRole(PtlRoles.SchemeAdmin))
            .AddPolicy(InternalUser, policy => policy.RequireRole(PtlRoles.InternalUser))
            .AddPolicy(Scheduling, policy => policy.RequireRole(PtlRoles.Scheduling))
            .AddPolicy(Distributions, policy => policy.RequireRole(PtlRoles.Distributions))
            .AddPolicy(TestConsultant, policy => policy.RequireRole(PtlRoles.TestConsultant))
            .AddPolicy(Assessor, policy => policy.RequireRole(PtlRoles.Assessor))
            .AddPolicy(ResultsSignOff, policy => policy.RequireRole(PtlRoles.ResultsSignOff))
            .AddPolicy(InvoiceGeneration, policy => policy.RequireRole(PtlRoles.ContractsAdmin).RequireRole(PtlRoles.GenerateInvoices))
            .AddPolicy(OverrideSchemes, policy => policy.RequireRole(PtlRoles.OverrideSchemes))
            .AddPolicy(Support, policy => policy.RequireRole(PtlRoles.Support));

        return services;
    }
}
