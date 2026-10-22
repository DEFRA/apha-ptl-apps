using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.TestConsultant;
using PTL.InternalWeb.Tests.TestSupport;
using static PTL.InternalWeb.Tests.TestSupport.TestConsultantTestData;

namespace PTL.InternalWeb.Tests.Features.TestConsultant;

public class TestConsultantControllerTests
{
    private readonly FakeTestConsultantTabulationApiClient _apiClient = new();

    private TestConsultantController CreateController() =>
        new(_apiClient) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private void SeedTabulation(bool submitted = false) =>
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation(submitted: submitted));

    private static TestConsultantTabulationViewModel Posted(string? comments = "<p>Mine</p>", bool filtered = false) =>
        new() { Comments = comments, IsFiltered = filtered, AllSamples = ["26/1", "26/2"], ShownSamples = ["26/1"], ShowMethodItems = false };

    // ---- Menu ---------------------------------------------------------------------------------

    [Fact]
    public async Task Index_ShowsTheConsultantsDistributionsOfTheLatestYear()
    {
        _apiClient.Distributions = new([Dist(2026, 6, "June"), Dist(2025, 1, "Old")]);

        var result = await CreateController().Index(null, CancellationToken.None);

        var model = Assert.IsType<TestConsultantMenuViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(2026, model.SelectedYear);
        Assert.Equal("June", Assert.Single(Assert.Single(model.Months).Distributions).DistributionTitle);
    }

    [Fact]
    public async Task Index_ApiRefusesTheConsultant_ShowsAccessDenied()
    {
        _apiClient.DistributionsException = new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden);
        var controller = CreateController();

        var result = await controller.Index(null, CancellationToken.None);

        Assert.Equal("AccessDenied", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Index_ApiFailsForAnotherReason_StillThrows()
    {
        _apiClient.DistributionsException = new HttpRequestException("Boom", null, HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateController().Index(null, CancellationToken.None));
    }

    [Fact]
    public async Task Index_RequestedYear_IsSelected()
    {
        _apiClient.Distributions = new([Dist(2026, 6, "June"), Dist(2025, 1, "Old")]);

        var model = Assert.IsType<TestConsultantMenuViewModel>(Assert.IsType<ViewResult>(await CreateController().Index(2025, CancellationToken.None)).Model);

        Assert.Equal(2025, model.SelectedYear);
    }

    // ---- Tabulation GET -----------------------------------------------------------------------

    [Fact]
    public async Task TabulationGet_ShowsTheUnfilteredTabulationWithTheSavedDraft()
    {
        SeedTabulation();

        var result = await CreateController().Tabulation(SchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestConsultantTabulationViewModel>(view.Model);
        Assert.Equal("Tabulation", view.ViewName);
        Assert.Equal((SchemeId, "<p>Draft</p>"), (model.MonthlyDistributionSchemeId, model.Comments));
        Assert.Equal(["26/1", "26/2"], model.ShownSamples);
        Assert.Equal(TestConsultantTabulationFilter.None, Assert.Single(_apiClient.TabulationFilters));
    }

    [Fact]
    public async Task TabulationGet_NotAllocated_ShowsTheAccessDeniedPage()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Forbidden);
        var controller = CreateController();

        var result = await controller.Tabulation(SchemeId, CancellationToken.None);

        Assert.Equal("AccessDenied", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
    }

    [Fact]
    public async Task TabulationGet_UnknownScheme_IsNotFound() =>
        Assert.IsType<NotFoundResult>(await CreateController().Tabulation(SchemeId, CancellationToken.None));

    // ---- Tabulation POST ----------------------------------------------------------------------

    [Fact]
    public async Task TabulationPost_NullModel_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateController().Tabulation(SchemeId, null!, "save", CancellationToken.None));

    [Fact]
    public async Task TabulationPost_Cancel_ReturnsToTheMenuWithoutSaving()
    {
        var result = await CreateController().Tabulation(SchemeId, Posted(), "CANCEL", CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(_apiClient.Saved);
        Assert.Empty(_apiClient.TabulationFilters);
    }

    [Fact]
    public async Task TabulationPost_Draw_RedrawsWithTheFilterAndKeepsTheTypedComments()
    {
        SeedTabulation();

        var result = await CreateController().Tabulation(SchemeId, Posted(comments: null, filtered: true), "draw", CancellationToken.None);

        var model = Assert.IsType<TestConsultantTabulationViewModel>(Assert.IsType<ViewResult>(result).Model);
        var filter = Assert.Single(_apiClient.TabulationFilters);
        Assert.Equal(["26/2"], filter.HiddenSamples);
        Assert.True(filter.HideMethodItems);
        Assert.Equal(["26/1"], model.ShownSamples);
        Assert.Empty(_apiClient.Saved);
    }

    [Fact]
    public async Task TabulationPost_DrawWithNoComments_IsNotValidated()
    {
        SeedTabulation();
        var controller = CreateController();
        controller.ModelState.AddModelError("Comments", "Comments required.");

        await controller.Tabulation(SchemeId, Posted(comments: null, filtered: true), "draw", CancellationToken.None);

        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task TabulationPost_Export_ReturnsTheWorkbookForTheFilter()
    {
        _apiClient.Export = new(TestConsultantApiStatus.Ok, new TabulationAttachmentFile([1, 2], "application/x-test", "attachment; filename=\"a.xlsx\""));
        var controller = CreateController();

        var result = await controller.Tabulation(SchemeId, Posted(filtered: true), "export", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/x-test", file.ContentType);
        Assert.Equal([1, 2], file.FileContents);
        Assert.Equal("attachment; filename=\"a.xlsx\"", controller.Response.Headers.ContentDisposition.ToString());
        Assert.Equal(["26/2"], Assert.Single(_apiClient.ExportFilters).HiddenSamples);
    }

    [Theory]
    [InlineData(TestConsultantApiStatus.Forbidden, "AccessDenied")]
    [InlineData(TestConsultantApiStatus.NotFound, null)]
    public async Task TabulationPost_ExportNotAllowed_IsRefused(TestConsultantApiStatus status, string? viewName)
    {
        _apiClient.Export = new(status);

        var result = await CreateController().Tabulation(SchemeId, Posted(), "export", CancellationToken.None);

        Assert.Equal(viewName, (result as ViewResult)?.ViewName);
        Assert.Equal(viewName is null, result is NotFoundResult);
    }

    [Theory]
    [InlineData("save", false)]
    [InlineData("submit", true)]
    [InlineData("SUBMIT", true)]
    public async Task TabulationPost_Save_SendsTheCommentsAndReturnsToTheMenu(string command, bool submit)
    {
        var result = await CreateController().Tabulation(SchemeId, Posted("<p>Final</p>"), command, CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        var (schemeId, request) = Assert.Single(_apiClient.Saved);
        Assert.Equal((SchemeId, "<p>Final</p>", submit), (schemeId, request.Comments, request.Submit));
    }

    [Fact]
    public async Task TabulationPost_InvalidComments_RedisplaysWithoutSaving()
    {
        SeedTabulation();
        var controller = CreateController();
        controller.ModelState.AddModelError("Comments", "Comments required.");

        var result = await controller.Tabulation(SchemeId, Posted(null), "submit", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Empty(_apiClient.Saved);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task TabulationPost_ServerRejectsTheComments_ShowsTheErrorOnTheField()
    {
        SeedTabulation();
        _apiClient.SaveResult = new(TestConsultantApiStatus.Ok, new TestConsultantTabulationSaveResult(false, false, new Dictionary<string, string> { ["Comments"] = "Too long" }));
        var controller = CreateController();

        var result = await controller.Tabulation(SchemeId, Posted(), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Too long", Assert.Single(controller.ModelState["Comments"]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task TabulationPost_AlreadySubmitted_ShowsALockedMessageAndTheReadOnlyComments()
    {
        SeedTabulation(submitted: true);
        _apiClient.SaveResult = new(TestConsultantApiStatus.Ok, new TestConsultantTabulationSaveResult(false, true, new Dictionary<string, string>()));
        var controller = CreateController();

        var result = await controller.Tabulation(SchemeId, Posted(), "save", CancellationToken.None);

        var model = Assert.IsType<TestConsultantTabulationViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.Tabulation!.Comments.IsSubmitted);
        Assert.Equal("These comments have already been submitted and can no longer be edited.", Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(TestConsultantApiStatus.Forbidden, "AccessDenied")]
    [InlineData(TestConsultantApiStatus.NotFound, null)]
    public async Task TabulationPost_SaveNotAllowed_IsRefused(TestConsultantApiStatus status, string? viewName)
    {
        _apiClient.SaveResult = new(status);
        var controller = CreateController();

        var result = await controller.Tabulation(SchemeId, Posted(), "save", CancellationToken.None);

        Assert.Equal(viewName, (result as ViewResult)?.ViewName);
        Assert.Equal(viewName is null, result is NotFoundResult);
    }

    [Theory]
    [InlineData(TestConsultantApiStatus.Forbidden)]
    [InlineData(TestConsultantApiStatus.NotFound)]
    public async Task TabulationPost_RedisplayNotAllowed_IsRefused(TestConsultantApiStatus status)
    {
        _apiClient.Tabulation = new(status);

        var result = await CreateController().Tabulation(SchemeId, Posted(), "draw", CancellationToken.None);

        Assert.True(status == TestConsultantApiStatus.Forbidden ? result is ViewResult { ViewName: "AccessDenied" } : result is NotFoundResult);
    }
}
