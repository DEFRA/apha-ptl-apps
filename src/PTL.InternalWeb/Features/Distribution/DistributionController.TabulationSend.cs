using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.Distribution;

// Tabulations "Send To Test Consultant" / "Send For Assessment" / "Send For Results Sign-Off" with
// Save and Cancel. As in legacy, a send (after its confirm() question) only changes what the page
// shows; Save writes every staged send, Cancel discards them. The staged sends travel in the form.
public sealed partial class DistributionController
{
    private const string SaveCommand = "save";

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public async Task<IActionResult> Tabulations(
        int year, int monthId, string? command, string? stage, string? confirmation, string[]? staged, CancellationToken cancellationToken)
    {
        var stagedSends = ParseStaged(staged);

        // Legacy ButtonSave/ButtonCancel both leave for the Distributions menu.
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { year });
        }

        if (string.Equals(command, SaveCommand, StringComparison.OrdinalIgnoreCase))
        {
            if (await distributionApiClient.SaveTabulationsAsync(year, monthId, stagedSends, cancellationToken))
            {
                TempData.SetNotification(NotificationType.Success, "Tabulations updated successfully.");
                return RedirectToAction(nameof(Index), new { year });
            }

            TempData.SetNotification(NotificationType.Error, "The changes could not be saved because the Tabulations have been changed by someone else. Please try again.");
            return RedirectToAction(nameof(Tabulations), new { year, monthId });
        }

        // Cancel on the confirmation page posts the form back declined: the send is dropped and the
        // page redisplays with only the earlier staged sends.
        if (!ConfirmationFlow.IsCancelled(confirmation) && ParseStaged(stage is null ? [] : [stage]) is [var send])
        {
            var sendConfirmation = await distributionApiClient.StageTabulationSendAsync(
                year, monthId, new TabulationStageRequest(stagedSends, send.MonthlyDistributionSchemeId, send.Command), cancellationToken);

            if (sendConfirmation is not null)
            {
                if (sendConfirmation.ConfirmationQuestion.Length > 0 && !ConfirmationFlow.IsConfirmed(confirmation))
                {
                    return this.ConfirmationPage(ToSendConfirmationPage(sendConfirmation), nameof(Tabulations));
                }

                stagedSends.Add(send);
            }
        }

        var page = await distributionApiClient.PreviewTabulationsAsync(year, monthId, stagedSends, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        return View(nameof(Tabulations), new TabulationsViewModel
        {
            YearId = year,
            MonthId = monthId,
            MonthDescription = PackagingMonthDescription(year, monthId),
            Schemes = page.Schemes,
            Staged = stagedSends,
        });
    }

    // "{schemeId}:{command}" values; anything malformed is ignored.
    private static List<TabulationStagedSendRequest> ParseStaged(IEnumerable<string>? values) =>
        [.. (values ?? [])
            .Select(v => v.Split(':', 2))
            .Where(p => p.Length == 2 && Guid.TryParse(p[0], out _) && p[1].Length > 0)
            .Select(p => new TabulationStagedSendRequest(Guid.Parse(p[0]), p[1]))];

    // The legacy confirm() question is the heading. Cancel posts the form back so earlier staged sends are kept.
    private static ConfirmationPageViewModel ToSendConfirmationPage(TabulationSendResponse send) => new()
    {
        Title = send.ConfirmationQuestion,
        PageTitle = send.ActionText,
        Caption = $"{send.DistributionReferenceFull} {send.SchemeNameFull}",
        IsWarningTitle = send.ConfirmationQuestion.Contains("Discard", StringComparison.Ordinal),
        ConfirmText = "OK",
    };
}
