using System.Security.Claims;
using PTL.InternalWeb.Authorization;

namespace PTL.InternalWeb.Features.TestConsultant;

// Legacy Web.config: location path="Test Consultant" allow roles="Test Consultant". Role membership is read from the
// ResolvedRoles claim InternalUserResolver adds at sign-in (PT-LIMS's own tblRoles, not an Entra ID group). Which
// schemes a Test Consultant may comment on is decided by PTL.Api from the scheme's allocated consultants.
public static class TestConsultantPolicy
{
    public const string Name = "TestConsultant";
    public const string RoleName = "Test Consultant";

    public static bool IsSatisfiedBy(ClaimsPrincipal user) => InternalRoleClaims.HasRole(user, RoleName);
}
