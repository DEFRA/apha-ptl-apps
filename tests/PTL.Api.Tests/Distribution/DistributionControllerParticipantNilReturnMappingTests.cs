using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Maps every outcome of the Nil Return service onto the API response flags.
public class DistributionControllerParticipantNilReturnMappingTests
{
    private static readonly Guid SchemeId = Guid.NewGuid();
    private static readonly Guid ParticipantId = Guid.NewGuid();

    private sealed class StubNilReturnService(ParticipantNilReturnData? data, ParticipantNilReturnSaveStatus status) : IParticipantNilReturnService
    {
        public string? SavedReason { get; private set; }

        public Task<ParticipantNilReturnData?> GetAsync(
            Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(data);

        public Task<ParticipantNilReturnSaveOutcome> SaveAsync(
            Guid distributionParticipantId, Guid monthlyDistributionSchemeId, string? nilReturnReason, CancellationToken cancellationToken = default)
        {
            SavedReason = nilReturnReason;
            return Task.FromResult(new ParticipantNilReturnSaveOutcome(status));
        }
    }

    private static ParticipantNilReturnController Controller(IParticipantNilReturnService service) => new(service);

    [Theory]
    [InlineData("No samples received", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public async Task Get_ReportsAReturnOnlyWhenAReasonIsSaved(string reason, bool expectedHasNilReturn)
    {
        var service = new StubNilReturnService(new ParticipantNilReturnData(ParticipantId, reason), ParticipantNilReturnSaveStatus.Saved);

        var result = await Controller(service).GetParticipantNilReturn(SchemeId, ParticipantId, CancellationToken.None);

        var payload = Assert.IsType<ParticipantNilReturnResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(expectedHasNilReturn, payload.HasNilReturn);
        Assert.Equal(reason, payload.NilReturnReason);
    }

    [Fact]
    public async Task Save_WhenTheServiceCannotFindTheParticipant_ReturnsNotFound()
    {
        var service = new StubNilReturnService(null, ParticipantNilReturnSaveStatus.NotFound);

        var result = await Controller(service).SaveParticipantNilReturn(
            SchemeId, ParticipantId, new ParticipantNilReturnSaveRequest("x"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData(ParticipantNilReturnSaveStatus.Saved, true, false, false, false)]
    [InlineData(ParticipantNilReturnSaveStatus.DeadlinePassed, false, true, false, false)]
    [InlineData(ParticipantNilReturnSaveStatus.ReasonRequired, false, false, true, false)]
    [InlineData(ParticipantNilReturnSaveStatus.TooLong, false, false, true, false)]
    [InlineData(ParticipantNilReturnSaveStatus.HasResults, false, false, false, true)]
    public async Task Save_SetsOnlyTheFlagThatMatchesTheOutcome(
        ParticipantNilReturnSaveStatus status, bool success, bool deadlinePassed, bool reasonRequired, bool hasResults)
    {
        var service = new StubNilReturnService(null, status);

        var result = await Controller(service).SaveParticipantNilReturn(
            SchemeId, ParticipantId, new ParticipantNilReturnSaveRequest("Lost in post"), CancellationToken.None);

        var payload = Assert.IsType<ParticipantNilReturnSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((success, deadlinePassed, reasonRequired, hasResults), (payload.Success, payload.DeadlinePassed, payload.ReasonRequired, payload.HasResults));
        Assert.Equal("Lost in post", service.SavedReason);
    }
}
