using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.TestConsultant;

// Test Consultant menu and tabulation - legacy Test Consultant/MenuTestConsultants.aspx and Tabulation.aspx. The
// Tabulation URL is the one the Test Consultant emails link to (TabulationEmailOptions.InternalCommentsPath).
// Which schemes a Test Consultant may open is decided by PTL.Api from the signed-in user, so a request for anyone
// else's scheme gets the access denied page. Save, Save and Submit and Cancel return to the menu, as legacy did.
[Authorize(Policy = TestConsultantPolicy.Name)]
[Route("Test Consultant")]
public sealed class TestConsultantController(ITestConsultantTabulationApiClient apiClient) : Controller
{
    private const string TabulationView = "Tabulation";
    private const string CancelCommand = "cancel";
    private const string DrawCommand = "draw";
    private const string ExportCommand = "export";
    private const string SubmitCommand = "submit";
    private const string CommentsField = nameof(TestConsultantTabulationViewModel.Comments);

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
        Guid monthlyDistributionSchemeId, TestConsultantTabulationViewModel model, string? command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index));
        }

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

        if (isSave && ModelState.IsValid)
        {
            var saved = await apiClient.SaveCommentsAsync(
                monthlyDistributionSchemeId,
                new TestConsultantTabulationSaveRequest(model.Comments, string.Equals(command, SubmitCommand, StringComparison.OrdinalIgnoreCase)),
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
        if (status == TestConsultantApiStatus.Forbidden)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("AccessDenied");
        }

        return NotFound();
    }
}
