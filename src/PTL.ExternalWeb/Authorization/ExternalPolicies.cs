using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Authorization;

// Named policies for the three external roles - mirrors PTL.InternalWeb.Authorization.PtlPolicies.
public static class ExternalPolicies
{
    public const string Participant = ExternalRoleNames.Participant;
    public const string Viewer = ExternalRoleNames.Viewer;
    public const string TestConsultant = ExternalRoleNames.TestConsultant;

    // Legacy Tabulations.aspx/AssessmentResults.aspx ("View Reports") - every resolved external
    // role can view published reports for the schemes/distributions they're linked to (the actual
    // data scoping happens server-side against spgTabulations, not via this policy).
    public const string ViewReports = "ViewReports";

    /// <summary>
    /// Registers the role claims transformation, the three policies above, the access-denied
    /// audit logger and a fallback policy that makes every endpoint require a signed-in user
    /// unless it opts out with [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AddExternalAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IClaimsTransformation, ExternalRoleClaimsTransformation>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ExternalAccessDeniedLoggingHandler>();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Participant, policy => policy.RequireRole(ExternalRoleNames.Participant))
            .AddPolicy(Viewer, policy => policy.RequireRole(ExternalRoleNames.Viewer))
            .AddPolicy(TestConsultant, policy => policy.RequireRole(ExternalRoleNames.TestConsultant))
            .AddPolicy(ViewReports, policy => policy.RequireRole(
                ExternalRoleNames.Participant, ExternalRoleNames.Viewer, ExternalRoleNames.TestConsultant));

        return services;
    }
}
