using PTL.Core.Contract.ImportPermit;

namespace PTL.Api.Tests.Contract;

public class ImportPermitServiceTests
{
    [Fact]
    public async Task GetByContractIdAsync_ReturnsRepositoryResults()
    {
        var repository = new FakeImportPermitRepository
        {
            Permits = [new ImportPermitEntity { ParticipantSchemeId = Guid.NewGuid(), SchemeNumber = "PT0001", SchemeName = "AHS", LabId = "1476" }]
        };
        var service = new ImportPermitService(repository);

        var result = await service.GetByContractIdAsync(Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal("PT0001", result[0].SchemeNumber);
    }

    [Fact]
    public async Task UpdateAsync_ReceivedWithExpiry_UpdatesRepository()
    {
        var repository = new FakeImportPermitRepository();
        var service = new ImportPermitService(repository);
        var participantSchemeId = Guid.NewGuid();
        var expiry = new DateTime(2027, 1, 1);

        await service.UpdateAsync(participantSchemeId, true, expiry);

        Assert.Single(repository.UpdateCalls);
        Assert.Equal((participantSchemeId, true, expiry), repository.UpdateCalls[0]);
    }

    [Fact]
    public async Task UpdateAsync_ReceivedWithoutExpiry_ThrowsValidationException()
    {
        var repository = new FakeImportPermitRepository();
        var service = new ImportPermitService(repository);

        var ex = await Assert.ThrowsAsync<ImportPermitValidationException>(
            () => service.UpdateAsync(Guid.NewGuid(), true, null));

        Assert.Equal("Import permit expiry date is required when permit is received.", ex.Message);
        Assert.Empty(repository.UpdateCalls);
    }

    [Fact]
    public async Task UpdateAsync_NotReceived_DoesNotRequireExpiry()
    {
        var repository = new FakeImportPermitRepository();
        var service = new ImportPermitService(repository);

        await service.UpdateAsync(Guid.NewGuid(), false, null);

        Assert.Single(repository.UpdateCalls);
    }
}
