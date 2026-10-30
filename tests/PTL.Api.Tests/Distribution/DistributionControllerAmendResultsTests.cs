using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.AmendResultsTestData;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

// Amend Results endpoints - the actions take IAmendResultsService via [FromServices].
public class DistributionControllerAmendResultsTests
{
    private readonly FakeDistributionRepository _repository = new();

    private AmendResultsService Service => new(_repository);

    private DistributionController CreateController(string? userName = null)
    {
        var httpContext = new DefaultHttpContext();
        if (userName is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], "Test"));
        }

        return new(
            new DistributionService(_repository),
            new SampleNumbersService(_repository, new FakeSchemeService()),
            new IntendedResultsService(_repository),
            new DistributionParticipantsService(_repository),
            new PreparationService(_repository),
            new ResultsReceiptService(_repository, TimeProvider.System),
            new TabulationsService(_repository, Clock()),
            new ActualResultsService(_repository, TimeProvider.System),
            new ParticipantDistributionHistoryService(_repository, new PTL.Api.Tests.Participant.FakeParticipantRepository(), new ActualResultsService(_repository, TimeProvider.System)),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private void Seed()
    {
        _repository.AmendResults[SchemeId] = Source();
        _repository.RatingOptions.AddRange(RatingOptions());
    }

    private static T OkValue<T>(ActionResult<T> result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public async Task GetAmendResults_MissingScheme_ReturnsNotFound()
    {
        var result = await CreateController().GetAmendResults(SchemeId, Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetAmendResults_ReturnsThePage()
    {
        Seed();

        var page = OkValue(await CreateController().GetAmendResults(SchemeId, Service, CancellationToken.None));

        Assert.Equal(Title, page.DistributionTitle);
        Assert.Equal(2, page.Tests.Count);
    }

    [Fact]
    public async Task SaveAmendResults_MissingScheme_ReturnsNotFound()
    {
        var result = await CreateController().SaveAmendResults(SchemeId, Request(auditWhy: "why"), Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SaveAmendResults_AuditsAsTheSignedInUser()
    {
        Seed();

        var result = await CreateController("DOMAIN\\tc").SaveAmendResults(
            SchemeId, Request(intendedMethods: [new(MethodRegex, Guid.Empty, "6", null)], auditWhy: "Typo"), Service, CancellationToken.None);

        Assert.True(OkValue(result).Success);
        var write = Assert.Single(_repository.SavedAmendResults);
        Assert.Equal(("DOMAIN\\tc", "Typo"), (write.AuditWho, write.AuditWhy));
    }

    [Fact]
    public async Task SaveAmendResults_NoUserName_AuditsAsGuest()
    {
        Seed();

        await CreateController().SaveAmendResults(SchemeId, Request(ratings: [new(Lab1, SampleA, Rating1)]), Service, CancellationToken.None);

        Assert.Equal("Guest", Assert.Single(_repository.SavedAmendResults).AuditWho);
    }

    [Fact]
    public async Task SaveAmendResults_AuditReasonAndFieldErrors_AreReturnedAsOk()
    {
        Seed();
        var controller = CreateController();

        var audit = OkValue(await controller.SaveAmendResults(
            SchemeId, Request(intendedMethods: [new(MethodRegex, Guid.Empty, "6", null)]), Service, CancellationToken.None));
        var invalid = OkValue(await controller.SaveAmendResults(
            SchemeId, Request(nilReturns: [new(Lab2, new string('x', 101))]), Service, CancellationToken.None));

        Assert.True(audit.AuditReasonRequired);
        Assert.Equal(AuditObject, audit.AuditObjectName);
        Assert.Equal([AmendResultsFieldKeys.NilReturn(Lab2)], invalid.FieldErrors.Keys);
        Assert.Empty(_repository.SavedAmendResults);
    }
}
