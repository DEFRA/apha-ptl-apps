using System.Collections.Frozen;

namespace PTL.InternalWeb.Authorization;

// Role-claim values issued for PTL.InternalWeb users. The roles themselves are the PTL database's
// (tblRoles / tlnkUserRoles, resolved at sign-in by InternalUserResolver) - never Entra ID groups or app roles.
public static class PtlRoles
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
    public const string GenerateInvoices = "GenerateInvoices";
    public const string OverrideSchemes = "OverrideSchemes";
    public const string Support = "Support";

    // Keys are the tblRoles names legacy checked with IsInRole.
    private static readonly FrozenDictionary<string, string> ByPtlRoleName = new Dictionary<string, string>
    {
        ["Admin"] = Admin,
        ["Contracts Admin"] = ContractsAdmin,
        ["Scheme Admin"] = SchemeAdmin,
        ["Internal User"] = InternalUser,
        ["Scheduling"] = Scheduling,
        ["Distributions"] = Distributions,
        ["Test Consultant"] = TestConsultant,
        ["Assessor"] = Assessor,
        ["Results Sign-off"] = ResultsSignOff,
        ["Generate Invoices"] = GenerateInvoices,
        ["Override Schemes"] = OverrideSchemes,
        ["Support"] = Support,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The role-claim value for a tblRoles role name, or null when the name is not a recognised PTL role.</summary>
    public static string? ToClaimValue(string ptlRoleName) =>
        ByPtlRoleName.GetValueOrDefault(ptlRoleName);
}
