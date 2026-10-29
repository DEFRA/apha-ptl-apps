using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Attaches a minimal resolved-Participant ClaimsPrincipal to a bare controller instance for
// unit tests that don't go through the full PtlExternalWebTestFactory HTTP pipeline.
internal static class AuthenticatedParticipant
{
    public static void Attach(Controller controller, Guid participantId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ExternalUserClaimTypes.ParticipantId, participantId.ToString())],
            authenticationType: "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }
}
