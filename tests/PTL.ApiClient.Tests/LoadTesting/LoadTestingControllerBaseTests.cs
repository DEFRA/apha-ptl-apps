using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PTL.Common.Auth;

namespace PTL.ApiClient.Tests.LoadTesting;

public class LoadTestingControllerBaseTests
{
    private const string DeniedError = "This user cannot sign in.";

    private readonly CapturingAuthenticationService _authentication = new();

    private TestController CreateController(bool enabled, Func<TestController, TestModel, Task<IActionResult>>? signIn = null) =>
        CreateController(new LoadTestingOptions { Enabled = enabled, ExpiresOn = FixedTimeProvider.LastDay }, FixedTimeProvider.BeforeExpiry, signIn);

    private TestController CreateController(LoadTestingOptions options, TimeProvider timeProvider, Func<TestController, TestModel, Task<IActionResult>>? signIn = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(_authentication);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        return new TestController(Options.Create(options), timeProvider, signIn)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NoTempDataProvider()),
            Url = new LocalOnlyUrlHelper(httpContext)
        };
    }

    private static Resolution Allowed(IReadOnlyDictionary<string, string>? claims) => new(true, claims);

    [Fact]
    public void LoginGet_WhenEnabled_ShowsTheFormKeepingReturnUrl()
    {
        var result = CreateController(enabled: true).Login("/dashboard");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("/dashboard", Assert.IsType<TestModel>(view.Model).ReturnUrl);
    }

    [Fact]
    public void LoginGet_WhenDisabled_IsNotFound()
    {
        Assert.IsType<NotFoundResult>(CreateController(enabled: false).Login("/dashboard"));
    }

    [Fact]
    public async Task LoginPost_WhenDisabled_IsNotFoundWithoutSigningIn()
    {
        var controller = CreateController(enabled: false);

        var result = await controller.Login(new TestModel(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, controller.SignInCalls);
    }

    [Fact]
    public async Task LoginPost_WhenEnabled_ReturnsTheDerivedSignInResult()
    {
        var expected = new OkResult();
        var controller = CreateController(enabled: true, (_, _) => Task.FromResult<IActionResult>(expected));

        var result = await controller.Login(new TestModel(), CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(1, controller.SignInCalls);
    }

    [Fact]
    public void LoginGet_OnTheLastDay_ShowsTheForm()
    {
        var controller = CreateController(new LoadTestingOptions { Enabled = true, ExpiresOn = FixedTimeProvider.LastDay }, FixedTimeProvider.LastMomentOfLastDay);

        Assert.IsType<ViewResult>(controller.Login("/dashboard"));
    }

    [Fact]
    public void LoginGet_AfterTheExpiryDate_IsNotFoundEvenWhenEnabled()
    {
        var controller = CreateController(new LoadTestingOptions { Enabled = true, ExpiresOn = FixedTimeProvider.LastDay }, FixedTimeProvider.DayAfterExpiry);

        Assert.IsType<NotFoundResult>(controller.Login("/dashboard"));
    }

    [Fact]
    public async Task LoginPost_AfterTheExpiryDate_IsNotFoundWithoutSigningIn()
    {
        var controller = CreateController(new LoadTestingOptions { Enabled = true, ExpiresOn = FixedTimeProvider.LastDay }, FixedTimeProvider.DayAfterExpiry);

        var result = await controller.Login(new TestModel(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, controller.SignInCalls);
    }

    [Fact]
    public void LoginGet_WithoutAnExpiryDate_IsNotFoundEvenWhenEnabled()
    {
        var controller = CreateController(new LoadTestingOptions { Enabled = true }, FixedTimeProvider.BeforeExpiry);

        Assert.IsType<NotFoundResult>(controller.Login("/dashboard"));
    }

    [Fact]
    public async Task CompleteSignIn_DeniedResolution_RedisplaysTheFormAsForbiddenWithoutSigningIn()
    {
        var model = new TestModel();
        var controller = CreateController(enabled: true, (c, m) => c.Complete(m, new Resolution(false, null), []));

        var result = await controller.Login(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Equal(DeniedError, model.Error);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Null(_authentication.SignedIn);
    }

    [Fact]
    public async Task CompleteSignIn_AllowedResolution_SignsInWithTheGivenAndResolvedClaimsThenGoesHome()
    {
        var controller = CreateController(enabled: true, (c, m) => c.Complete(
            m,
            Allowed(new Dictionary<string, string> { [ClaimTypes.Role] = "Admin" }),
            [new Claim(ClaimTypes.NameIdentifier, "user-1")]));

        var result = await controller.Login(new TestModel(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(("Index", "Home"), (redirect.ActionName, redirect.ControllerName));
        var (scheme, principal) = _authentication.SignedIn!.Value;
        Assert.Equal("Cookies", scheme);
        Assert.Equal("Cookies", principal.Identity!.AuthenticationType);
        Assert.Equal("user-1", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Admin", principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public async Task CompleteSignIn_AllowedResolutionWithNoExtraClaims_SignsInWithTheGivenClaimsOnly()
    {
        var controller = CreateController(enabled: true, (c, m) => c.Complete(m, Allowed(null), [new Claim(ClaimTypes.NameIdentifier, "user-1")]));

        await controller.Login(new TestModel(), CancellationToken.None);

        var claim = Assert.Single(_authentication.SignedIn!.Value.Principal.Claims);
        Assert.Equal((ClaimTypes.NameIdentifier, "user-1"), (claim.Type, claim.Value));
    }

    [Fact]
    public async Task CompleteSignIn_LocalReturnUrl_IsHonoured()
    {
        var controller = CreateController(enabled: true, (c, m) => c.Complete(m, Allowed(null), []));

        var result = await controller.Login(new TestModel { ReturnUrl = "/Contract" }, CancellationToken.None);

        Assert.Equal("/Contract", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Theory]
    [InlineData("https://evil.test/")]
    [InlineData("")]
    public async Task CompleteSignIn_ExternalOrEmptyReturnUrl_GoesHome(string returnUrl)
    {
        var controller = CreateController(enabled: true, (c, m) => c.Complete(m, Allowed(null), []));

        var result = await controller.Login(new TestModel { ReturnUrl = returnUrl }, CancellationToken.None);

        Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(result).ControllerName);
    }

    [Fact]
    public async Task Fail_RedisplaysTheFormWithTheErrorAndStatus()
    {
        var model = new TestModel();
        var controller = CreateController(enabled: true, (c, m) => Task.FromResult<IActionResult>(c.CallFail(m, StatusCodes.Status503ServiceUnavailable)));

        var result = await controller.Login(model, CancellationToken.None);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, controller.Response.StatusCode);
        Assert.Equal(TestController.ApiUnavailable, model.Error);
    }

    [Fact]
    public void Routing_IsAnonymousUnderPerfAndThePostRequiresAnAntiforgeryToken()
    {
        var type = typeof(LoadTestingControllerBase<TestModel>);
        var post = type.GetMethod(nameof(LoadTestingControllerBase<TestModel>.Login), [typeof(TestModel), typeof(CancellationToken)])!;
        var get = type.GetMethod(nameof(LoadTestingControllerBase<TestModel>.Login), [typeof(string)])!;

        Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("perf", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("login", get.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("login", post.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.NotNull(post.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Null(post.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>());
    }

    public sealed class TestModel : LoadTestingLoginViewModelBase;

    private sealed record Resolution(bool IsAllowed, IReadOnlyDictionary<string, string>? Claims) : IIdentityResolution
    {
        public string? DenialRedirectPath => null;
    }

    private sealed class TestController(IOptions<LoadTestingOptions> options, TimeProvider timeProvider, Func<TestController, TestModel, Task<IActionResult>>? signIn)
        : LoadTestingControllerBase<TestModel>(options, timeProvider)
    {
        public const string ApiUnavailable = ApiUnavailableError;

        public int SignInCalls { get; private set; }

        public Task<IActionResult> Complete(TestModel model, IIdentityResolution resolution, List<Claim> claims) =>
            CompleteSignInAsync(model, resolution, claims, DeniedError);

        public ViewResult CallFail(TestModel model, int statusCode) => Fail(model, statusCode, ApiUnavailableError);

        protected override Task<IActionResult> SignInAsync(TestModel model, CancellationToken cancellationToken)
        {
            SignInCalls++;
            return signIn is null ? Task.FromResult<IActionResult>(new EmptyResult()) : signIn(this, model);
        }
    }

    private sealed class CapturingAuthenticationService : IAuthenticationService
    {
        public (string? Scheme, ClaimsPrincipal Principal)? SignedIn { get; private set; }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignedIn = (scheme, principal);
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class NoTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    private sealed class LocalOnlyUrlHelper(HttpContext httpContext)
        : UrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
    {
        public override bool IsLocalUrl(string? url) => url is not null && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal);
    }
}
