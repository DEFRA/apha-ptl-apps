using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.InternalWeb.Authorization;

namespace PTL.InternalWeb.Features.Distribution;

// Tabulation, Issued Tabulations, Issued Tabulation and the (Issued) Assessment Results chart -
// legacy Scheduling/Tabulation.aspx, PublishedTabulations.aspx, PublishedTabulation.aspx,
// AssessmentResults.aspx and PublishedAssessmentResults.aspx. year/monthId (and tabulationId on the
// issued pages) are carried so Back and the breadcrumbs return to the page the user came from.
public sealed partial class DistributionController
{
    [HttpGet]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public async Task<IActionResult> Tabulation(
        int year, int monthId, Guid tabulationId, Guid? participantId, Guid? viewerId, CancellationToken cancellationToken)
    {
        var report = await distributionApiClient.GetTabulationAsync(tabulationId, participantId ?? Guid.Empty, viewerId ?? Guid.Empty, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        return View(new TabulationReportViewModel { YearId = year, MonthId = monthId, TabulationId = tabulationId, Report = report });
    }

    [HttpGet]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public async Task<IActionResult> PublishedTabulations(int year, int monthId, Guid tabulationId, CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetPublishedTabulationsAsync(tabulationId, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        return View(new PublishedTabulationsViewModel { YearId = year, MonthId = monthId, TabulationId = tabulationId, Page = page });
    }

    [HttpGet]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public async Task<IActionResult> PublishedTabulation(
        int year, int monthId, Guid tabulationId, Guid publishedTabulationId, Guid? participantId, Guid? viewerId, CancellationToken cancellationToken)
    {
        var report = await distributionApiClient.GetPublishedTabulationAsync(publishedTabulationId, participantId ?? Guid.Empty, viewerId ?? Guid.Empty, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        return View(new TabulationReportViewModel
        {
            YearId = year,
            MonthId = monthId,
            TabulationId = tabulationId,
            PublishedTabulationId = publishedTabulationId,
            Report = report,
        });
    }

    [HttpGet]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public async Task<IActionResult> TabulationAssessmentResults(int year, int monthId, Guid tabulationId, Guid testId, CancellationToken cancellationToken)
    {
        var results = await distributionApiClient.GetAssessmentResultsAsync(tabulationId, testId, false, cancellationToken);
        if (results is null)
        {
            return NotFound();
        }

        return View("AssessmentResults", new AssessmentResultsViewModel
        {
            YearId = year,
            MonthId = monthId,
            TabulationId = tabulationId,
            TestId = testId,
            Results = results,
        });
    }

    [HttpGet]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public async Task<IActionResult> PublishedAssessmentResults(
        int year, int monthId, Guid tabulationId, Guid publishedTabulationId, Guid testId, CancellationToken cancellationToken)
    {
        var results = await distributionApiClient.GetAssessmentResultsAsync(publishedTabulationId, testId, true, cancellationToken);
        if (results is null)
        {
            return NotFound();
        }

        return View("AssessmentResults", new AssessmentResultsViewModel
        {
            YearId = year,
            MonthId = monthId,
            TabulationId = tabulationId,
            PublishedTabulationId = publishedTabulationId,
            TestId = testId,
            Results = results,
        });
    }

    // Legacy AttachmentDisplay "View Supporting Document" (Attachment.aspx), without the session hand-off.
    [HttpGet]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public Task<IActionResult> TabulationAttachment(Guid tabulationId, Guid attachmentId, CancellationToken cancellationToken) =>
        AttachmentFile(tabulationId, false, attachmentId, cancellationToken);

    [HttpGet]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public Task<IActionResult> PublishedTabulationAttachment(Guid publishedTabulationId, Guid attachmentId, CancellationToken cancellationToken) =>
        AttachmentFile(publishedTabulationId, true, attachmentId, cancellationToken);

    private async Task<IActionResult> AttachmentFile(Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await distributionApiClient.GetTabulationAttachmentAsync(tabulationId, isPublished, attachmentId, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        Response.Headers.ContentDisposition = file.ContentDisposition;
        return File(file.Content, file.ContentType);
    }
}
