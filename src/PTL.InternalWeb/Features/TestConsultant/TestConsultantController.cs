using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;

namespace PTL.InternalWeb.Features.TestConsultant;

// Test Consultant menu and tabulation - legacy Test Consultant/MenuTestConsultants.aspx and Tabulation.aspx. The
// Tabulation URL is the one the Test Consultant emails link to (TabulationEmailOptions.InternalCommentsPath).
// Which schemes a Test Consultant may open is decided by PTL.Api from the signed-in user, so a request for anyone
// else's scheme gets the shared Access Denied page. Save and Save and Submit return to the menu, as legacy did, and Cancel and Back are links to it.
[Authorize(Policy = PtlPolicies.TestConsultant)]
[Route("Test Consultant")]
public sealed class TestConsultantController(ITestConsultantTabulationApiClient apiClient) : Controller
{
    private const string TabulationView = "Tabulation";
    private const string DrawCommand = "draw";
    private const string ExportCommand = "export";
    private const string SubmitCommand = "submit";
    private const string CommentsField = nameof(TestConsultantTabulationViewModel.Comments);

    public const string SaveQuestion = "Are you sure you wish to save your comments?";
    public const string SubmitQuestion = "Once your comments are submitted, you will not be able to edit them again. Are you sure you wish to submit?";

    [HttpGet("")]
    public async Task<IActionResult> Index(int? year, CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.GetDistributionsAsync(cancellationToken);
            return View(TestConsultantMenuViewModel.From(response, year));
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            // PTL.Api refuses an inactive Test Consultant (and an unidentified caller).
            return NotOk(TestConsultantApiStatus.Forbidden);
        }
    }

    [HttpGet("Tabulation")]
    public async Task<IActionResult> Tabulation(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        var model = new TestConsultantTabulationViewModel();
        var result = await apiClient.GetTabulationAsync(monthlyDistributionSchemeId, TestConsultantTabulationFilter.None, cancellationToken);
        if (result.Value is { } tabulation && result.IsOk)
        {
            model.Display(monthlyDistributionSchemeId, tabulation);
            model.Comments = tabulation.Comments.CommentsHtml;
            return View(TabulationView, model);
        }

        return NotOk(result.Status);
    }

    [HttpPost("Tabulation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Tabulation(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationViewModel model, string? command, string? confirmation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.Equals(command, ExportCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await ExportAsync(monthlyDistributionSchemeId, model, cancellationToken);
        }

        var isSave = !string.Equals(command, DrawCommand, StringComparison.OrdinalIgnoreCase);
        if (!isSave)
        {
            // Drawing the tabulations only refreshes the tables: the comments typed so far are kept, not validated.
            ModelState.Remove(CommentsField);
        }

        // Cancel on the confirmation page posts the form back declined: nothing is saved and the page
        // redisplays with the comments typed so far.
        if (isSave && ModelState.IsValid && !ConfirmationFlow.IsCancelled(confirmation))
        {
            var submit = string.Equals(command, SubmitCommand, StringComparison.OrdinalIgnoreCase);

            if (!ConfirmationFlow.IsConfirmed(confirmation))
            {
                return this.ConfirmationPage(ToConfirmationPage(submit), nameof(Tabulation));
            }

            var saved = await apiClient.SaveCommentsAsync(
                monthlyDistributionSchemeId,
                new TestConsultantTabulationSaveRequest(model.Comments, submit),
                cancellationToken);
            if (!saved.IsOk || saved.Value is null)
            {
                return NotOk(saved.Status);
            }

            if (saved.Value.Success)
            {
                return RedirectToAction(nameof(Index));
            }

            AddSaveErrors(saved.Value);
        }

        return await RedisplayAsync(monthlyDistributionSchemeId, model, cancellationToken);
    }

    // Shared confirmation page, as Distribution's send for Results Sign-Off. The submit question is the legacy
    // BtnSubmit confirm() text, shown as a warning because a submission cannot be undone.
    private static ConfirmationPageViewModel ToConfirmationPage(bool submit) => new()
    {
        Title = submit ? SubmitQuestion : SaveQuestion,
        PageTitle = submit ? "Save and Submit comments" : "Save comments",
        IsWarningTitle = submit,
        ConfirmText = "OK",
    };

    private async Task<IActionResult> ExportAsync(Guid monthlyDistributionSchemeId, TestConsultantTabulationViewModel model, CancellationToken cancellationToken)
    {
        var result = await apiClient.ExportTabulationAsync(monthlyDistributionSchemeId, model.ToFilter(), cancellationToken);
        if (!result.IsOk || result.Value is null)
        {
            return NotOk(result.Status);
        }

        Response.Headers.ContentDisposition = result.Value.ContentDisposition;
        return File(result.Value.Content, result.Value.ContentType);
    }

    private async Task<IActionResult> RedisplayAsync(Guid monthlyDistributionSchemeId, TestConsultantTabulationViewModel model, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetTabulationAsync(monthlyDistributionSchemeId, model.ToFilter(), cancellationToken);
        if (!result.IsOk || result.Value is null)
        {
            return NotOk(result.Status);
        }

        model.Display(monthlyDistributionSchemeId, result.Value);
        return View(TabulationView, model);
    }

    private void AddSaveErrors(TestConsultantTabulationSaveResult result)
    {
        if (result.IsLocked)
        {
            ModelState.AddModelError(string.Empty, "These comments have already been submitted and can no longer be edited.");
            return;
        }

        foreach (var (_, message) in result.Errors)
        {
            ModelState.AddModelError(CommentsField, message);
        }
    }

    private IActionResult NotOk(TestConsultantApiStatus status)
    {
        // Not allocated to the scheme, or an inactive Test Consultant: the same refusal as any role-gated page,
        // which the cookie scheme sends to the shared Access Denied page (legacy had one for the whole site).
        if (status == TestConsultantApiStatus.Forbidden)
        {
            return Forbid();
        }

        return NotFound();
    }
}
