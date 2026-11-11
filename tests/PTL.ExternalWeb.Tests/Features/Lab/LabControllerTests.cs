using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using PTL.Auth.Cidm.Claims;
using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Features.Lab;
using PTL.ExternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Tests.Features.Lab;

public class LabControllerTests
{
    private static readonly Guid ContactId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid LabA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid LabB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly FakeExternalUserApiClient _api = new(ResolveExternalUserResponse.Denied(ExternalUserResolutionStatus.NotFound, string.Empty));
    private readonly FakeAuthenticationService _auth = new();

    private static ClaimsPrincipal User(Guid? contactId = null, Guid? currentLab = null, int labCount = 2)
    {
        var claims = new List<Claim>
        {
            new(ExternalUserClaimTypes.UserType, "Participant"),
            new(ExternalUserClaimTypes.LabCount, labCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("keep-me", "yes")
        };
        if (contactId is { } id)
        {
            claims.Add(new Claim(CidmClaimTypes.ContactId, id.ToString()));
        }

        if (currentLab is { } lab)
        {
            claims.Add(new Claim(ExternalUserClaimTypes.ParticipantId, lab.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private LabController CreateController(ClaimsPrincipal user, AuthenticationProperties? properties = null)
    {
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(_auth).BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services, User = user };
        _auth.AuthenticateResult = AuthenticateResult.Success(new AuthenticationTicket(user, properties ?? new AuthenticationProperties(), CookieAuthenticationDefaults.AuthenticationScheme));
        return new LabController(_api)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider()),
            Url = new FakeUrlHelper()
        };
    }

    private void AddLab(Guid id, string code, string name, bool canOrderOnline = false)
    {
        _api.Labs.Add(new ExternalLab(id, code, name));
        _api.LabContexts.Add(new ExternalLabContext(id, code, name, canOrderOnline));
    }

    private static string? ErrorFor(Controller controller) =>
        controller.ModelState[nameof(LabSelectionViewModel.SelectedLabId)]?.Errors.FirstOrDefault()?.ErrorMessage;

    // ---- the selection page ----

    [Fact]
    public async Task Select_SeveralLabs_ShowsThemAllWithNoneChosenForAFirstSelection()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var controller = CreateController(User(ContactId));

        var result = await controller.Select(CancellationToken.None);

        var model = Assert.IsType<LabSelectionViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal([LabA, LabB], model.Labs.Select(lab => lab.ParticipantId));
        Assert.Null(model.SelectedLabId);
        Assert.False(model.IsSwitch);
        Assert.Equal([ContactId], _api.GetLabsContactIds);
    }

    [Fact]
    public async Task Select_SwitchingFromALab_PreselectsTheCurrentLab()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var controller = CreateController(User(ContactId, currentLab: LabB));

        var model = Assert.IsType<LabSelectionViewModel>(Assert.IsType<ViewResult>(await controller.Select(CancellationToken.None)).Model);

        Assert.Equal(LabB, model.SelectedLabId);
        Assert.True(model.IsSwitch);
    }

    [Fact]
    public async Task Select_CurrentLabNoLongerOneOfTheUsers_PreselectsNothing()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var controller = CreateController(User(ContactId, currentLab: Guid.NewGuid()));

        var model = Assert.IsType<LabSelectionViewModel>(Assert.IsType<ViewResult>(await controller.Select(CancellationToken.None)).Model);

        Assert.Null(model.SelectedLabId);
    }

    [Fact]
    public async Task Select_OnlyOneLabLeft_SelectsItAndGoesHome()
    {
        AddLab(LabA, "0142", "Bristol", canOrderOnline: true);
        var controller = CreateController(User(ContactId));

        var result = await controller.Select(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(("Index", "Home"), (redirect.ActionName, redirect.ControllerName));
        var (_, principal, _) = Assert.Single(_auth.SignIns);
        Assert.Equal(LabA, principal.GetParticipantId());
        Assert.Equal("1", principal.FindFirst(ExternalUserClaimTypes.LabCount)?.Value);
        Assert.True(principal.CanOrderOnline());
    }

    [Fact]
    public async Task Select_OnlyOneLabButTheApiRefusesIt_IsTreatedAsNoAccess()
    {
        _api.Labs.Add(new ExternalLab(LabA, "0142", "Bristol"));
        var controller = CreateController(User(ContactId));

        var result = await controller.Select(CancellationToken.None);

        Assert.Equal("/Account/NotPermitted?reason=no-active-lab", Assert.IsType<RedirectResult>(result).Url);
        Assert.Empty(_auth.SignIns);
    }

    [Fact]
    public async Task Select_NoActiveLabs_GoesToTheNoActiveLabPage()
    {
        var controller = CreateController(User(ContactId));

        var result = await controller.Select(CancellationToken.None);

        Assert.Equal("/Account/NotPermitted?reason=no-active-lab", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task Select_NoContactIdOnTheSession_GoesToTheNoActiveLabPageWithoutCallingTheApi()
    {
        var controller = CreateController(User(contactId: null));

        var get = await controller.Select(CancellationToken.None);
        var post = await controller.Select(new LabSelectionInput { SelectedLabId = LabA }, CancellationToken.None);

        Assert.IsType<RedirectResult>(get);
        Assert.IsType<RedirectResult>(post);
        Assert.Empty(_api.GetLabsContactIds);
    }

    // ---- choosing ----

    [Fact]
    public async Task Select_Post_ChosenLab_ReissuesTheCookieForThatLabKeepingTheTokensAndOtherClaims()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern", canOrderOnline: true);
        var properties = new AuthenticationProperties { IsPersistent = true };
        properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = "t" }]);
        var controller = CreateController(User(ContactId), properties);

        var result = await controller.Select(new LabSelectionInput { SelectedLabId = LabB }, CancellationToken.None);

        Assert.Equal(("Index", "Home"), (Assert.IsType<RedirectToActionResult>(result).ActionName, Assert.IsType<RedirectToActionResult>(result).ControllerName));
        var (scheme, principal, signInProperties) = Assert.Single(_auth.SignIns);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, scheme);
        Assert.Equal(LabB, principal.GetParticipantId());
        Assert.Equal("0187", principal.FindFirst(ExternalUserClaimTypes.LabCode)?.Value);
        Assert.Equal("2", principal.FindFirst(ExternalUserClaimTypes.LabCount)?.Value);
        Assert.True(principal.CanOrderOnline());
        Assert.Equal("yes", principal.FindFirst("keep-me")?.Value);
        Assert.Equal(ContactId, principal.GetContactId());
        Assert.Same(properties, signInProperties);
        Assert.Equal("t", signInProperties!.GetTokenValue("id_token"));
        Assert.Equal((ContactId, LabB), Assert.Single(_api.GetLabCalls));
    }

    [Fact]
    public async Task Select_Post_FirstSelection_ShowsNoNotification()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var controller = CreateController(User(ContactId));

        await controller.Select(new LabSelectionInput { SelectedLabId = LabA }, CancellationToken.None);

        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Select_Post_SwitchingToAnotherLab_ConfirmsWithASuccessNotification()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var controller = CreateController(User(ContactId, currentLab: LabA));

        await controller.Select(new LabSelectionInput { SelectedLabId = LabB }, CancellationToken.None);

        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("You are now working on lab 0187 – Northern.", notification.Message);
    }

    [Fact]
    public async Task Select_Post_ChoosingTheCurrentLabAgain_ShowsNoNotification()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var controller = CreateController(User(ContactId, currentLab: LabA));

        await controller.Select(new LabSelectionInput { SelectedLabId = LabA }, CancellationToken.None);

        Assert.Null(controller.TempData.GetNotification());
        Assert.Single(_auth.SignIns);
    }

    [Fact]
    public async Task Select_Post_NothingChosen_AsksAgainWithoutSigningInOrCallingTheLabLookup()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var controller = CreateController(User(ContactId, currentLab: LabA));

        var result = await controller.Select(new LabSelectionInput(), CancellationToken.None);

        var model = Assert.IsType<LabSelectionViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Select a lab", ErrorFor(controller));
        Assert.Equal(2, model.Labs.Count);
        Assert.True(model.IsSwitch);
        Assert.Empty(_auth.SignIns);
        Assert.Empty(_api.GetLabCalls);
    }

    // The same message for a lab id that is not the user's, so the form cannot be used to find out which labs exist.
    [Fact]
    public async Task Select_Post_ALabThatIsNotTheUsers_IsRefusedLikeNothingChosen()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var foreign = Guid.NewGuid();
        var controller = CreateController(User(ContactId));

        var result = await controller.Select(new LabSelectionInput { SelectedLabId = foreign }, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Select a lab", ErrorFor(controller));
        Assert.Empty(_auth.SignIns);
        Assert.Equal((ContactId, foreign), Assert.Single(_api.GetLabCalls));
    }

    [Fact]
    public async Task Select_Post_NoActiveLabsAnyMore_GoesToTheNoActiveLabPage()
    {
        var controller = CreateController(User(ContactId));

        var result = await controller.Select(new LabSelectionInput { SelectedLabId = LabA }, CancellationToken.None);

        Assert.Equal("/Account/NotPermitted?reason=no-active-lab", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task Select_NoSessionToReissue_DoesNothingRatherThanThrowing()
    {
        AddLab(LabA, "0142", "Bristol");
        AddLab(LabB, "0187", "Northern");
        var controller = CreateController(User(ContactId));
        _auth.AuthenticateResult = AuthenticateResult.NoResult();

        var result = await controller.Select(new LabSelectionInput { SelectedLabId = LabA }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(_auth.SignIns);
    }
}
