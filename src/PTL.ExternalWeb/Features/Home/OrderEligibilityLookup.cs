using System.Security.Claims;
using Polly;
using PTL.ApiClient;
using PTL.Contracts.CreateOrder;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Home;

// Shared by the Home cards and the header menu so both read the per-year "order already submitted" state the same way.
public static class OrderEligibilityLookup
{
    // The order pages enforce the rules themselves, so a failed lookup shows both years rather than breaking the page.
    public static readonly CreateOrderEligibilityResponse AllowBoth = new(true, true, true);

    public static async Task<CreateOrderEligibilityResponse> GetAsync(
        ClaimsPrincipal user, ICreateOrderApiClient createOrderApiClient, CancellationToken cancellationToken)
    {
        var participantId = user.GetParticipantId();
        if (participantId is null || !user.CanOrderOnline())
        {
            return AllowBoth;
        }

        try
        {
            return await createOrderApiClient.GetEligibilityAsync(participantId.Value, cancellationToken) ?? AllowBoth;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ExecutionRejectedException)
        {
            return AllowBoth;
        }
    }
}
