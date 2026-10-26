using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Features.Contract;
using PTL.InternalWeb.Features.Customer;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Features.GroupAddress;
using PTL.InternalWeb.Features.Invoice;
using PTL.InternalWeb.Features.Participant;
using PTL.InternalWeb.Features.ParticipantScheme;
using PTL.InternalWeb.Features.Scheme;
using PTL.InternalWeb.Features.SchemeAdmin;
using PTL.InternalWeb.Features.Search;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Features.TestConsultant;

namespace PTL.InternalWeb.Tests.Authorization;

// The Page Authorization Matrix, enforced: every public action of every role-gated controller must carry exactly the
// policies the legacy folder rules call for, so a new action cannot ship unclassified.
public class AuthorizationMatrixTests
{
    private static readonly string[] DistributionsOnly =
        ["Index", "ViewStage", "Search", "ParticipantAddressLabels", "PrintParticipantAddressLabel", "GroupAddressLabels", "PrintGroupAddressLabel"];

    // Legacy Scheduling/ folder pages and the Scheduling-only dashboard actions.
    private static readonly string[] SchedulingActions =
    [
        "InitialiseMonth", "Schedule", "ToggleCancelled", "SampleNumbers", "IntendedResults", "Participants",
        "ChangeDeadline", "EditDeadline", "RepeatDistribution", "Tabulations", "Tabulation", "PublishedTabulations", "PublishedTabulation",
        "TabulationAssessmentResults", "PublishedAssessmentResults", "TabulationAttachment", "PublishedTabulationAttachment",
        "ViewComments", "ViewCommentsUpload", "ViewCommentsRemoveAttachment", "ViewCommentsAttachment",
        "ViewCommentsStagedAttachment", "AmendResults"
    ];

    // Legacy Internal User/ folder pages.
    private static readonly string[] InternalUserActions =
    [
        "Preparation", "PrintableResults", "Packaging", "ResultsReceipt", "SampleReceipt", "ActualResults"
    ];

    public static TheoryData<Type, string> ControllerBaselines => new()
    {
        { typeof(SystemAdministrationController), PtlPolicies.Admin },
        { typeof(CustomerController), PtlPolicies.ContractsAdmin },
        { typeof(ParticipantController), PtlPolicies.ContractsAdmin },
        { typeof(ContractController), PtlPolicies.ContractsAdmin },
        { typeof(ParticipantSchemeController), PtlPolicies.ContractsAdmin },
        { typeof(GroupAddressController), PtlPolicies.ContractsAdmin },
        { typeof(SearchController), PtlPolicies.ContractsAdmin },
        { typeof(InvoiceController), PtlPolicies.InvoiceGeneration },
        { typeof(SchemeController), PtlPolicies.SchemeAdmin },
        { typeof(SchemeAdminController), PtlPolicies.SchemeAdmin },
        { typeof(TestConsultantController), PtlPolicies.TestConsultant },
    };

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName
                && method.GetCustomAttribute<NonActionAttribute>() is null
                && IsActionResult(method.ReturnType));

    private static bool IsActionResult(Type returnType)
    {
        var result = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;
        return typeof(IActionResult).IsAssignableFrom(result);
    }

    private static string[] Policies(MemberInfo member) =>
        [.. member.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Policy!).Where(p => p is not null).Order()];

    [Theory]
    [MemberData(nameof(ControllerBaselines))]
    public void EveryAction_CarriesTheControllersLegacyRolePolicy(Type controller, string policy)
    {
        Assert.Equal([policy], Policies(controller));

        foreach (var action in Actions(controller))
        {
            Assert.Empty(Policies(action));
            Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        }

        Assert.NotEmpty(Actions(controller));
    }

    [Fact]
    public void DistributionController_BaselineIsTheDistributionsRole()
    {
        Assert.Equal([PtlPolicies.Distributions], Policies(typeof(DistributionController)));
    }

    [Fact]
    public void DistributionActions_FollowTheLegacySchedulingAndInternalUserFolders()
    {
        var actions = Actions(typeof(DistributionController)).ToList();

        foreach (var action in actions)
        {
            var expected = action.Name switch
            {
                var name when SchedulingActions.Contains(name) => new[] { PtlPolicies.Scheduling },
                var name when InternalUserActions.Contains(name) => new[] { PtlPolicies.InternalUser },
                var name when DistributionsOnly.Contains(name) => Array.Empty<string>(),
                _ => throw new Xunit.Sdk.XunitException($"Distribution action '{action.Name}' is not classified in the authorization matrix.")
            };

            Assert.True(
                expected.SequenceEqual(Policies(action)),
                $"Distribution.{action.Name} should require [{string.Join(", ", expected)}] but has [{string.Join(", ", Policies(action))}].");
        }

        var names = actions.Select(a => a.Name).ToHashSet();
        Assert.All(SchedulingActions.Concat(InternalUserActions).Concat(DistributionsOnly), name => Assert.Contains(name, names));
    }

    [Fact]
    public void SystemAdministration_IsAdminOnly_NotJustAnySignedInUser()
    {
        // Restores the Admin gate that the testing-phase bypass had replaced with RequireAuthenticatedUser.
        Assert.Equal([PtlPolicies.Admin], Policies(typeof(SystemAdministrationController)));
    }
}
