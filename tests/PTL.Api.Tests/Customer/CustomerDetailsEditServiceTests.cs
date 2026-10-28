using PTL.Api.Tests.Participant;
using PTL.Core.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Customer;

public class CustomerDetailsEditServiceTests
{
    private static (CustomerDetailsEditService Service, FakeParticipantRepository Participants, FakeCustomerRepository Customers, FakePendingCustomerUpdateRepository PendingUpdates) CreateService()
    {
        var participants = new FakeParticipantRepository();
        var customers = new FakeCustomerRepository();
        var pendingUpdates = new FakePendingCustomerUpdateRepository();
        var service = new CustomerDetailsEditService(participants, customers, pendingUpdates);
        return (service, participants, customers, pendingUpdates);
    }

    [Fact]
    public async Task GetAsync_ParticipantNotFound_ReturnsNull()
    {
        var (service, _, _, _) = CreateService();

        var result = await service.GetAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_CustomerNotFound_ReturnsNull()
    {
        var (service, participants, _, _) = CreateService();
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = Guid.NewGuid() });

        var result = await service.GetAsync(participant.ParticipantId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_NoOutstandingPendingUpdate_ReturnsLiveCustomerValues()
    {
        var (service, participants, customers, _) = CreateService();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId, QalNumber = "QAL/00001", ContactName = "Live Contact" });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });

        var result = await service.GetAsync(participant.ParticipantId);

        Assert.NotNull(result);
        Assert.False(result!.HasOutstandingUpdate);
        Assert.Equal("QAL/00001", result.QalNumber);
        Assert.Equal("Live Contact", result.Values.ContactName);
    }

    [Fact]
    public async Task GetAsync_OutstandingPendingUpdateExists_ReturnsPendingValues()
    {
        var (service, participants, customers, pendingUpdates) = CreateService();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId, QalNumber = "QAL/00001", ContactName = "Live Contact" });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });
        await pendingUpdates.UpsertAsync(new PendingCustomerUpdate { CustomerId = customerId, ContactName = "Pending Contact", IsSubmitted = true });

        var result = await service.GetAsync(participant.ParticipantId);

        Assert.NotNull(result);
        Assert.True(result!.HasOutstandingUpdate);
        Assert.Equal("Pending Contact", result.Values.ContactName);
    }

    [Fact]
    public async Task SubmitAsync_InvalidData_ThrowsCustomerValidationExceptionAndDoesNotPersist()
    {
        var (service, participants, customers, pendingUpdates) = CreateService();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });

        await Assert.ThrowsAsync<CustomerValidationException>(() =>
            service.SubmitAsync(participant.ParticipantId, new PendingCustomerUpdate()));

        Assert.Null(await pendingUpdates.GetByCustomerIdAsync(customerId));
    }

    [Fact]
    public async Task SubmitAsync_OutstandingPendingUpdateAlreadyExists_ThrowsAndDoesNotOverwrite()
    {
        var (service, participants, customers, pendingUpdates) = CreateService();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });
        await pendingUpdates.UpsertAsync(new PendingCustomerUpdate { CustomerId = customerId, ContactName = "Already Submitted", IsSubmitted = true });

        await Assert.ThrowsAsync<PendingCustomerUpdateAlreadySubmittedException>(() =>
            service.SubmitAsync(participant.ParticipantId, new PendingCustomerUpdate { ContactName = "New Attempt" }));

        var stillStored = await pendingUpdates.GetByCustomerIdAsync(customerId);
        Assert.Equal("Already Submitted", stillStored!.ContactName);
    }

    [Fact]
    public async Task SubmitAsync_ValidDataAndNoOutstandingUpdate_PersistsWithResolvedCustomerIdAndSubmittedFlag()
    {
        var (service, participants, customers, pendingUpdates) = CreateService();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = customerId });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });

        var proposed = new PendingCustomerUpdate
        {
            ContactName = "Jane Doe",
            Organisation = "Test Lab",
            Address1 = "1 Test Street",
            Address2 = "Testville",
            CountryId = Guid.NewGuid(),
            Telephone = "01234 567890",
            Email = "jane@example.com",
            InvoiceOrganisation = "Test Lab Invoicing",
            InvoiceAddress1 = "1 Test Street",
            InvoiceAddress2 = "Testville",
            InvoiceCountryId = Guid.NewGuid()
        };

        await service.SubmitAsync(participant.ParticipantId, proposed);

        var stored = await pendingUpdates.GetByCustomerIdAsync(customerId);
        Assert.NotNull(stored);
        Assert.Equal(customerId, stored!.CustomerId);
        Assert.True(stored.IsSubmitted);
        Assert.False(stored.IsDeleted);
        Assert.Equal("Jane Doe", stored.ContactName);
    }

    [Fact]
    public async Task SubmitAsync_FaxFieldsNotOnEditForm_CarriesOverLiveCustomerValues()
    {
        var (service, participants, customers, pendingUpdates) = CreateService();
        var customerId = Guid.NewGuid();
        await customers.CreateAsync(new PTL.Core.Customer.Customer
        {
            CustomerId = customerId,
            Fax = "01234 111111",
            InvoiceFax = "01234 222222"
        });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customerId });

        var proposed = new PendingCustomerUpdate
        {
            ContactName = "Jane Doe",
            Organisation = "Test Lab",
            Address1 = "1 Test Street",
            Address2 = "Testville",
            CountryId = Guid.NewGuid(),
            Telephone = "01234 567890",
            Email = "jane@example.com",
            InvoiceOrganisation = "Test Lab Invoicing",
            InvoiceAddress1 = "1 Test Street",
            InvoiceAddress2 = "Testville",
            InvoiceCountryId = Guid.NewGuid()
        };

        await service.SubmitAsync(participant.ParticipantId, proposed);

        var stored = await pendingUpdates.GetByCustomerIdAsync(customerId);
        Assert.NotNull(stored);
        Assert.Equal("01234 111111", stored!.Fax);
        Assert.Equal("01234 222222", stored.InvoiceFax);
    }
}
