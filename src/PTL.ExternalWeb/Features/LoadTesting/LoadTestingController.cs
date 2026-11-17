using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.ApiClient;
using PTL.Auth.Cidm.Claims;
using PTL.Auth.Cidm.Events;
using PTL.Contracts.LoadTesting;
using PTL.ExternalWeb.Features.Lab;

namespace PTL.ExternalWeb.Features.LoadTesting;

/// <summary>
/// Email-only sign-in for JMeter runs. Uses the contact id PTL.Api already stores for the email and resolves it through the same
/// <see cref="ICidmExternalUserResolver"/> as a real CIDM sign-in, so the session carries identical claims and no user record is
/// written. 404 unless <see cref="LoadTestingOptions.Enabled"/>.
/// </summary>
[SkipLabSelection]
public sealed class LoadTestingController(
    IOptions<LoadTestingOptions> options,
    ILoadTestingLookupClient lookupClient,
    ICidmExternalUserResolver resolver) : LoadTestingControllerBase<LoadTestingLoginViewModel>(options)
{
    protected override async Task<IActionResult> SignInAsync(LoadTestingLoginViewModel model, CancellationToken cancellationToken)
    {
        var email = model.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
        {
            return Fail(model, StatusCodes.Status400BadRequest, "Enter an email address.");
        }

        var lookup = await lookupClient.LookupExternalUserAsync(email, cancellationToken);
        if (lookup is null)
        {
            return Fail(model, StatusCodes.Status503ServiceUnavailable, ApiUnavailableError);
        }

        if (lookup.Status != LoadTestingLookupStatus.Found || lookup.SsoIdExt is not { } contactId)
        {
            return Fail(model, StatusCodes.Status403Forbidden, lookup.Status switch
            {
                LoadTestingLookupStatus.NotLinked => "This user has no stored contact id (fldSsoIdExt); it must be set in the database first.",
                LoadTestingLookupStatus.Ambiguous => "This email's records carry more than one contact id.",
                _ => "No PTL external user has this email."
            });
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CidmClaimTypes.ContactId, contactId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim("name", email)
        ]));

        var resolution = await resolver.ResolveAsync(principal, cancellationToken);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, contactId.ToString()),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email),
            new(CidmClaimTypes.ContactId, contactId.ToString())
        };

        return await CompleteSignInAsync(model, resolution, claims, "This user cannot sign in (inactive, no active lab, or not resolved).");
    }
}

public sealed class LoadTestingLoginViewModel : LoadTestingLoginViewModelBase
{
    public string? Email { get; set; }
}
