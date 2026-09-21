using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Participant;
using PTL.Contracts.Participant;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Endpoints;

public class ParticipantControllerTests
{
    private static ParticipantController CreateController(FakeParticipantRepository repository)
    {
        var controller = new ParticipantController(new ParticipantService(repository, NullLogger<ParticipantService>.Instance), NullLogger<ParticipantController>.Instance);

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    private static ParticipantRequest ValidCreateRequest(Guid customerId, string labName = "Sample Lab") => new(
        CustomerId: customerId,
        SsoId: Guid.NewGuid(),
        LabCode: "LAB-001",
        LabName: labName,
        LabTypeId: Guid.NewGuid(),
        ContactName: "Alice Example",
        Organisation: labName,
        Address1: "1 Sample Street",
        Address2: "Sample District",
        Address3: string.Empty,
        Address4: string.Empty,
        Address5: string.Empty,
        CountryId: Guid.NewGuid(),
        Telephone: "01234 567890",
        Fax: string.Empty,
        Email: "alice@example.com",
        Email2: string.Empty,
        Comments: string.Empty,
        IsActive: true);

    private static ParticipantRequest ToUpdateRequest(ParticipantRequest request) => request;

    [Fact]
    public async Task CreateParticipant_ValidRequest_ReturnsCreatedAtAction()
    {
        var controller = CreateController(new FakeParticipantRepository());

        var result = await controller.CreateParticipant(ValidCreateRequest(Guid.NewGuid()), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ParticipantController.GetParticipant), created.ActionName);
        Assert.IsType<ParticipantResponse>(created.Value);
    }

    [Fact]
    public async Task GetParticipant_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantRepository());

        var result = await controller.GetParticipant(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetParticipant_ExistingParticipant_ReturnsOk()
    {
        var repository = new FakeParticipantRepository();
        var controller = CreateController(repository);
        var customerId = Guid.NewGuid();
        var created = await controller.CreateParticipant(ValidCreateRequest(customerId), CancellationToken.None);
        var participantId = ((ParticipantResponse)((CreatedAtActionResult)created.Result!).Value!).ParticipantId;

        var result = await controller.GetParticipant(participantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(participantId, ((ParticipantResponse)ok.Value!).ParticipantId);
    }

    [Fact]
    public async Task GetParticipants_ReturnsSummaryList()
    {
        var controller = CreateController(new FakeParticipantRepository());
        var customerId = Guid.NewGuid();
        await controller.CreateParticipant(ValidCreateRequest(customerId, "Alpha Lab"), CancellationToken.None);
        await controller.CreateParticipant(ValidCreateRequest(customerId, "Beta Lab"), CancellationToken.None);

        var result = await controller.GetParticipants(customerId, includeInactive: false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summaries = Assert.IsAssignableFrom<IReadOnlyList<ParticipantSummaryResponse>>(ok.Value);
        Assert.Equal(2, summaries.Count);
    }

    [Fact]
    public async Task SearchParticipants_FiltersAndPagesResults()
    {
        var controller = CreateController(new FakeParticipantRepository());
        var customerId = Guid.NewGuid();
        await controller.CreateParticipant(ValidCreateRequest(customerId, "Alpha Lab"), CancellationToken.None);
        await controller.CreateParticipant(ValidCreateRequest(customerId, "Beta Lab"), CancellationToken.None);

        var result = await controller.SearchParticipants(customerId, "Beta", includeInactive: false, page: 1, pageSize: 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ParticipantSearchResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal("Beta Lab", response.Items[0].LabName);
    }

    [Fact]
    public async Task UpdateParticipant_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantRepository());

        var result = await controller.UpdateParticipant(Guid.NewGuid(), ToUpdateRequest(ValidCreateRequest(Guid.NewGuid())), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateParticipant_ExistingParticipant_ReturnsOkWithUpdatedFields()
    {
        var repository = new FakeParticipantRepository();
        var controller = CreateController(repository);
        var customerId = Guid.NewGuid();
        var created = await controller.CreateParticipant(ValidCreateRequest(customerId), CancellationToken.None);
        var participantId = ((ParticipantResponse)((CreatedAtActionResult)created.Result!).Value!).ParticipantId;
        var update = ToUpdateRequest(ValidCreateRequest(customerId, "Updated Lab"));

        var result = await controller.UpdateParticipant(participantId, update, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Updated Lab", ((ParticipantResponse)ok.Value!).LabName);
    }

    [Fact]
    public async Task DeactivateParticipant_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantRepository());

        var result = await controller.DeactivateParticipant(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeactivateParticipant_ExistingParticipant_ReturnsOkWithInactiveParticipant()
    {
        var repository = new FakeParticipantRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateParticipant(ValidCreateRequest(Guid.NewGuid()), CancellationToken.None);
        var participantId = ((ParticipantResponse)((CreatedAtActionResult)created.Result!).Value!).ParticipantId;

        var result = await controller.DeactivateParticipant(participantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.False(((ParticipantResponse)ok.Value!).IsActive);
    }

    [Fact]
    public async Task ReactivateParticipant_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantRepository());

        var result = await controller.ReactivateParticipant(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ReactivateParticipant_ExistingParticipant_ReturnsOkWithActiveParticipant()
    {
        var repository = new FakeParticipantRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateParticipant(ValidCreateRequest(Guid.NewGuid()), CancellationToken.None);
        var participantId = ((ParticipantResponse)((CreatedAtActionResult)created.Result!).Value!).ParticipantId;
        await controller.DeactivateParticipant(participantId, CancellationToken.None);

        var result = await controller.ReactivateParticipant(participantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(((ParticipantResponse)ok.Value!).IsActive);
    }
}
