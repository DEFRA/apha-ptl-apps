using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Authorization;

// Named policies for the external roles - mirrors PTL.InternalWeb.Authorization.PtlPolicies.
public static class ExternalPolicies
{
    public const string Participant = ExternalRoleNames.Participant;
    public const string Viewer = ExternalRoleNames.Viewer;
    public const string TestConsultant = ExternalRoleNames.TestConsultant;
    public const string LabUser = ExternalRoleNames.LabUser;

    /// <summary>
    /// The lab's own primary contact: a Participant who is not a Lab User. A Lab User holds the Participant role too (so every
    /// participant page works for them), which is why this policy excludes them explicitly - it guards what only the primary
    /// contact may do: edit the customer and participant details, and manage the lab's users.
    /// </summary>
    public const string PrimaryContact = nameof(PrimaryContact);

    /// <summary>
    /// Registers the role claims transformation, the policies above, the access-denied
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
            .AddPolicy(LabUser, policy => policy.RequireRole(ExternalRoleNames.LabUser))
            .AddPolicy(PrimaryContact, policy => policy
                .RequireRole(ExternalRoleNames.Participant)
                .RequireAssertion(context => !context.User.IsLabUser()));

        return services;
    }
}
