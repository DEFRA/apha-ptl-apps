using Microsoft.Extensions.Logging.Abstractions;
using PTL.Core.Participant;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Participant;

public class ParticipantServiceTests
{
    private static ParticipantService CreateService(FakeParticipantRepository repository) =>
        new(repository, NullLogger<ParticipantService>.Instance);

    private static ParticipantEntity ValidActiveParticipant(string labName = "Alpha Lab") => new()
    {
        CustomerId = Guid.NewGuid(),
        SsoId = Guid.NewGuid(),
        LabCode = "001",
        LabName = labName,
        LabTypeId = Guid.NewGuid(),
        ContactName = "Alice Example",
        Organisation = "Alpha Organisation",
        Address1 = "1 Sample Street",
        Address2 = "Sample District",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890",
        Email = "alice@example.com",
        IsActive = true
    };

    [Fact]
    public async Task CreateParticipantAsync_ValidParticipant_PersistsAndAssignsId()
    {
        var service = CreateService(new FakeParticipantRepository());

        var created = await service.CreateParticipantAsync(ValidActiveParticipant());

        Assert.NotEqual(Guid.Empty, created.ParticipantId);
        Assert.True(created.IsActive);
        Assert.Equal("001", created.LabCode);
    }

    [Fact]
    public async Task DeactivateParticipantAsync_SetsInactiveAndStampsInactiveDate()
    {
        var repository = new FakeParticipantRepository();
        var service = CreateService(repository);
        var created = await service.CreateParticipantAsync(ValidActiveParticipant());

        var deactivated = await service.DeactivateParticipantAsync(created.ParticipantId);

        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsActive);
        Assert.NotNull(deactivated.InactiveDate);
    }
}
