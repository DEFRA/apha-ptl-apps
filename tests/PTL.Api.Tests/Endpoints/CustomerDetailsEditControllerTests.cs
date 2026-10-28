using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Participant;
using PTL.Contracts.Customer;
using PTL.Core.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Endpoints;

public class CustomerDetailsEditControllerTests
{
    private static (CustomerDetailsEditController Controller, FakeParticipantRepository Participants, FakeCustomerRepository Customers, FakePendingCustomerUpdateRepository PendingUpdates) CreateController()
    {
        var participants = new FakeParticipantRepository();
        var customers = new FakeCustomerRepository();
        var pendingUpdates = new FakePendingCustomerUpdateRepository();
        var service = new CustomerDetailsEditService(participants, customers, pendingUpdates);
        return (new CustomerDetailsEditController(service), participants, customers, pendingUpdates);
    }

    [Fact]
    public async Task Get_ParticipantNotFound_ReturnsNotFound()
    {
        var (controller, _, _, _) = CreateController();

        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Get_ParticipantResolvesToCustomer_ReturnsMappedResponse()
    {
        var (controller, participants, customers, _) = CreateController();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId, ContactName = "Jane Doe" });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });

        var result = await controller.Get(participant.ParticipantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CustomerDetailsEditResponse>(ok.Value);
        Assert.Equal("Jane Doe", response.ContactName);
        Assert.False(response.HasOutstandingUpdate);
    }

    [Fact]
    public async Task Submit_ValidRequest_ReturnsSuccessResult()
    {
        var (controller, participants, customers, _) = CreateController();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });

        var request = new CustomerDetailsEditSaveRequest(
            "Jane Doe", "Test Lab", "1 Test Street", "Testville", "", "", "",
            Guid.NewGuid(), "01234 567890", "", "jane@example.com",
            "", "Test Lab Invoicing", "1 Test Street", "Testville", "", "", "",
            Guid.NewGuid(), "", "", "");

        var result = await controller.Submit(participant.ParticipantId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var saveResult = Assert.IsType<CustomerDetailsEditSaveResult>(ok.Value);
        Assert.True(saveResult.Success);
    }

    [Fact]
    public async Task Submit_InvalidRequest_ReturnsFailureResultWithFieldErrors()
    {
        var (controller, participants, customers, _) = CreateController();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });

        var request = new CustomerDetailsEditSaveRequest(
            "", "", "", "", "", "", "", Guid.Empty, "", "", "",
            "", "", "", "", "", "", "", Guid.Empty, "", "", "");

        var result = await controller.Submit(participant.ParticipantId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var saveResult = Assert.IsType<CustomerDetailsEditSaveResult>(ok.Value);
        Assert.False(saveResult.Success);
        Assert.Contains("ContactName", saveResult.Errors.Keys);
    }

    [Fact]
    public async Task Submit_OutstandingPendingUpdateAlreadyExists_ReturnsFailureResult()
    {
        var (controller, participants, customers, pendingUpdates) = CreateController();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });
        await pendingUpdates.UpsertAsync(new PendingCustomerUpdate { CustomerId = customerId, IsSubmitted = true });

        var request = new CustomerDetailsEditSaveRequest(
            "Jane Doe", "Test Lab", "1 Test Street", "Testville", "", "", "",
            Guid.NewGuid(), "01234 567890", "", "jane@example.com",
            "", "Test Lab Invoicing", "1 Test Street", "Testville", "", "", "",
            Guid.NewGuid(), "", "", "");

        var result = await controller.Submit(participant.ParticipantId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var saveResult = Assert.IsType<CustomerDetailsEditSaveResult>(ok.Value);
        Assert.False(saveResult.Success);
    }
}
