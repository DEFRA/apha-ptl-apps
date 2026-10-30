using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Features.Home;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Home;

public class HomeControllerTests
{
    private static HomeController CreateController(string? importantMessage = null, FakeCreateOrderApiClient? createOrderApiClient = null, params Claim[] claims) =>
        new(new FakeApiClient(), new FakeSystemMessageApiClient(importantMessage), createOrderApiClient ?? new FakeCreateOrderApiClient())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };

    [Fact]
    public async Task Index_DisplayNameAndResolvedRolesClaimsPresent_ShowsBoth()
    {
        var controller = CreateController(claims:
        [
            new Claim(ExternalUserClaimTypes.DisplayName, "Jane Doe"),
            new Claim(ExternalUserClaimTypes.ResolvedRoles, "Viewer,Participant")
        ]);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Equal("Jane Doe", model.DisplayName);
        Assert.Equal(["Viewer", "Participant"], model.Roles);
    }

    [Fact]
    public async Task Index_NoResolvedRolesClaim_ShowsNoRoles()
    {
        var controller = CreateController(claims: new Claim(ExternalUserClaimTypes.DisplayName, "Jane Doe"));

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Empty(model.Roles);
    }

    [Fact]
    public async Task Index_ResolvedRolesClaim_SetsMatchingRoleFlags()
    {
        var controller = CreateController(claims: new Claim(ExternalUserClaimTypes.ResolvedRoles, "Participant,Test Consultant"));

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.True(model.IsParticipant);
        Assert.True(model.IsTestConsultant);
        Assert.False(model.IsViewer);
    }

    [Fact]
    public async Task Index_NoDisplayNameClaim_FallsBackToIdentityName()
    {
        var controller = CreateController(claims: new Claim(ClaimTypes.Name, "fallback-name"));

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Equal("fallback-name", model.DisplayName);
    }

    [Fact]
    public async Task Index_NoDisplayNameOrNameClaim_ShowsEmptyDisplayName()
    {
        var controller = CreateController();

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Equal(string.Empty, model.DisplayName);
    }

    [Fact]
    public async Task Index_ImportantMessagePublished_PassesHtmlToViewModel()
    {
        var controller = CreateController(importantMessage: "<p>Planned maintenance <strong>Friday</strong></p>");

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Equal("<p>Planned maintenance <strong>Friday</strong></p>", model.ImportantMessageHtml);
    }

    [Fact]
    public async Task Index_NoImportantMessagePublished_ViewModelHasNullMessage()
    {
        var controller = CreateController();

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Null(model.ImportantMessageHtml);
    }

    private static readonly Guid ParticipantId = Guid.NewGuid();

    private static Claim[] OrderingParticipantClaims() =>
    [
        new Claim(ExternalUserClaimTypes.ResolvedRoles, "Participant"),
        new Claim(ExternalUserClaimTypes.ParticipantId, ParticipantId.ToString()),
        new Claim(ExternalUserClaimTypes.CanOrderOnline, "true")
    ];

    [Fact]
    public async Task Index_ParticipantWhoCanOrder_PassesPerYearEligibilityToViewModel()
    {
        var fake = new FakeCreateOrderApiClient { Eligibility = new(true, false, true) };
        var controller = CreateController(createOrderApiClient: fake, claims: OrderingParticipantClaims());

        var result = await controller.Index();

        var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.CanCreateCurrentYearOrder);
        Assert.True(model.CanCreateNextYearOrder);
    }

    [Fact]
    public async Task Index_ParticipantWithoutCanOrderOnlineClaim_SkipsEligibilityLookup()
    {
        var fake = new FakeCreateOrderApiClient { EligibilityException = new InvalidOperationException("must not be called") };
        var controller = CreateController(createOrderApiClient: fake, claims:
        [
            new Claim(ExternalUserClaimTypes.ResolvedRoles, "Participant"),
            new Claim(ExternalUserClaimTypes.ParticipantId, ParticipantId.ToString())
        ]);

        var result = await controller.Index();

        var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.CanCreateCurrentYearOrder);
        Assert.True(model.CanCreateNextYearOrder);
    }

    [Fact]
    public async Task Index_EligibilityLookupFails_StillShowsBothOrderCards()
    {
        var fake = new FakeCreateOrderApiClient { EligibilityException = new HttpRequestException("api down") };
        var controller = CreateController(createOrderApiClient: fake, claims: OrderingParticipantClaims());

        var result = await controller.Index();

        var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.CanCreateCurrentYearOrder);
        Assert.True(model.CanCreateNextYearOrder);
    }

    [Fact]
    public async Task Index_ParticipantUnknownToApi_StillShowsBothOrderCards()
    {
        var fake = new FakeCreateOrderApiClient { Eligibility = null };
        var controller = CreateController(createOrderApiClient: fake, claims: OrderingParticipantClaims());

        var result = await controller.Index();

        var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.CanCreateCurrentYearOrder);
        Assert.True(model.CanCreateNextYearOrder);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var controller = new HomeController(new FakeApiClient(), new FakeSystemMessageApiClient(), new FakeCreateOrderApiClient());

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Information_MessagePublished_PassesHtmlToViewModel()
    {
        var controller = new HomeController(new FakeApiClient(), new FakeSystemMessageApiClient(message: "<p>Welcome</p>"), new FakeCreateOrderApiClient())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Information();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InformationViewModel>(viewResult.Model);
        Assert.Equal("<p>Welcome</p>", model.MessageHtml);
    }

    [Fact]
    public async Task Information_NoMessagePublished_ViewModelHasNullMessage()
    {
        var controller = new HomeController(new FakeApiClient(), new FakeSystemMessageApiClient(), new FakeCreateOrderApiClient())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Information();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InformationViewModel>(viewResult.Model);
        Assert.Null(model.MessageHtml);
    }

    [Fact]
    public async Task ApiStatus_ReturnsJsonFromApiClient()
    {
        var expected = new ApiHealthResponse("Healthy", 42, DateTime.UtcNow);
        var controller = new HomeController(new FakeApiClient(expected), new FakeSystemMessageApiClient(), new FakeCreateOrderApiClient());

        var result = await controller.ApiStatus(CancellationToken.None);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(expected, jsonResult.Value);
    }

    [Fact]
    public void Error_ReturnsViewWithRequestId()
    {
        var controller = new HomeController(new FakeApiClient(), new FakeSystemMessageApiClient(), new FakeCreateOrderApiClient())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.Error();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.True(model.ShowRequestId);
    }
}
