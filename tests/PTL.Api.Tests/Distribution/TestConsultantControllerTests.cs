using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Audit;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TestConsultantTestData;

namespace PTL.Api.Tests.Distribution;

public class TestConsultantControllerTests
{
    private sealed class FakeActingUser(Guid? id) : IActingUserAccessor
    {
        public Guid? ActingUserId { get; } = id;
    }

    private readonly FakeTestConsultantTabulationRepository _repository = new();
    private readonly FakeDistributionRepository _distributionRepository = new();

    private TestConsultantController Controller(Guid? actingUser = null, bool anonymous = false) =>
        new(new TestConsultantTabulationService(_repository, _distributionRepository), new FakeActingUser(anonymous ? null : actingUser ?? ConsultantId));

    private void SeedScheme(bool submitted = false)
    {
        _repository.States[SchemeId] = State(submitted: submitted);
        _repository.Tabulations[(SchemeId, false)] = Entity(submitted);
        _repository.Tabulations[(SchemeId, true)] = Entity(submitted);
    }

    private static int? Status(IActionResult result) => (result as StatusCodeResult)?.StatusCode;

    [Fact]
    public async Task GetDistributions_ListsTheActingUsersDistributions()
    {
        _repository.Distributions.Add(new TestConsultantDistributionEntity { YearId = 2026, MonthId = 6 });

        var result = await Controller().GetDistributions(CancellationToken.None);

        var response = Assert.IsType<TestConsultantDistributionsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Single(response.Distributions);
        Assert.Equal(ConsultantId, _repository.DistributionsRequestedFor);
    }

    [Fact]
    public async Task GetDistributions_WithoutAnActingUser_IsForbidden()
    {
        var result = await Controller(anonymous: true).GetDistributions(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Status(result.Result!));
        Assert.Null(_repository.DistributionsRequestedFor);
    }

    [Fact]
    public async Task GetTabulation_AllocatedConsultant_ReturnsOkWithTheTabulation()
    {
        SeedScheme();

        var result = await Controller().GetTabulation(SchemeId, null, false, CancellationToken.None);

        var response = Assert.IsType<TestConsultantTabulationResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("FINAL REPORT for D1a/S1 BT: Brucella", response.Title);
    }

    [Fact]
    public async Task GetTabulation_PassesTheFilterOn()
    {
        SeedScheme();

        var result = await Controller().GetTabulation(SchemeId, ["26/2"], true, CancellationToken.None);

        var response = Assert.IsType<TestConsultantTabulationResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(["PCR", "26/1"], response.ResultsTables[0].Rows[0].Cells.Select(c => c.Text));
    }

    [Fact]
    public async Task GetTabulation_UnknownScheme_IsNotFound() =>
        Assert.IsType<NotFoundResult>((await Controller().GetTabulation(SchemeId, null, false, CancellationToken.None)).Result);

    [Fact]
    public async Task GetTabulation_OtherConsultant_IsForbidden()
    {
        SeedScheme();

        var result = await Controller(OtherConsultantId).GetTabulation(SchemeId, null, false, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Status(result.Result!));
    }

    [Fact]
    public async Task GetTabulation_WithoutAnActingUser_IsForbidden()
    {
        SeedScheme();

        Assert.Equal(StatusCodes.Status403Forbidden, Status((await Controller(anonymous: true).GetTabulation(SchemeId, null, false, CancellationToken.None)).Result!));
    }

    [Fact]
    public async Task ExportTabulation_ReturnsTheWorkbookFile()
    {
        SeedScheme();

        var result = await Controller().ExportTabulation(SchemeId, null, false, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal((TabulationWorkbookWriter.ContentType, "D1a_S1_BT.xlsx"), (file.ContentType, file.FileDownloadName));
    }

    [Fact]
    public async Task ExportTabulation_NotAllowed_IsRefused()
    {
        SeedScheme();

        Assert.Equal(StatusCodes.Status403Forbidden, Status(await Controller(OtherConsultantId).ExportTabulation(SchemeId, null, false, CancellationToken.None)));
        Assert.Equal(StatusCodes.Status403Forbidden, Status(await Controller(anonymous: true).ExportTabulation(SchemeId, null, false, CancellationToken.None)));
        Assert.IsType<NotFoundResult>(await Controller().ExportTabulation(Guid.NewGuid(), null, false, CancellationToken.None));
    }

    [Fact]
    public async Task SaveComments_SavesTheDraft()
    {
        SeedScheme();

        var result = await Controller().SaveComments(SchemeId, new("<p>Hi</p>", false), CancellationToken.None);

        Assert.True(Assert.IsType<TestConsultantTabulationSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value).Success);
        Assert.Single(_distributionRepository.UpdatedComments);
    }

    [Fact]
    public async Task SaveComments_Locked_ReportsItWithoutWriting()
    {
        SeedScheme(submitted: true);

        var result = await Controller().SaveComments(SchemeId, new("<p>Hi</p>", true), CancellationToken.None);

        Assert.True(Assert.IsType<TestConsultantTabulationSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value).IsLocked);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    [Fact]
    public async Task InactiveConsultant_IsRefusedOnEveryEndpointAndNothingIsWritten()
    {
        SeedScheme();
        _repository.InactiveConsultants.Add(ConsultantId);

        Assert.Equal(StatusCodes.Status403Forbidden, Status((await Controller().GetDistributions(CancellationToken.None)).Result!));
        Assert.Equal(StatusCodes.Status403Forbidden, Status((await Controller().GetTabulation(SchemeId, null, false, CancellationToken.None)).Result!));
        Assert.Equal(StatusCodes.Status403Forbidden, Status(await Controller().ExportTabulation(SchemeId, null, false, CancellationToken.None)));
        Assert.Equal(StatusCodes.Status403Forbidden, Status((await Controller().SaveComments(SchemeId, new("<p>Hi</p>", true), CancellationToken.None)).Result!));
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    [Fact]
    public async Task SaveComments_NotAllowed_IsRefusedAndWritesNothing()
    {
        SeedScheme();

        Assert.Equal(StatusCodes.Status403Forbidden, Status((await Controller(OtherConsultantId).SaveComments(SchemeId, new("<p>Hi</p>", true), CancellationToken.None)).Result!));
        Assert.Equal(StatusCodes.Status403Forbidden, Status((await Controller(anonymous: true).SaveComments(SchemeId, new("<p>Hi</p>", true), CancellationToken.None)).Result!));
        Assert.IsType<NotFoundResult>((await Controller().SaveComments(Guid.NewGuid(), new("<p>Hi</p>", true), CancellationToken.None)).Result);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }
}
