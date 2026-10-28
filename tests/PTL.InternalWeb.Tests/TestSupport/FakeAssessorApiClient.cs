using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeAssessorApiClient : IAssessorApiClient
{
    public AssessorApiResult<AssessorMenuResponse> Menu { get; set; } = new(AssessorApiStatus.Ok, new AssessorMenuResponse([], 0, []));

    public AssessorApiResult<AssessorItemListResponse> ItemList { get; set; } = new(AssessorApiStatus.NotFound);

    public AssessorApiResult<AssessorEntryResponse> Entry { get; set; } = new(AssessorApiStatus.NotFound);

    public AssessorSaveApiResult SaveResult { get; set; } = AssessorSaveApiResult.Of(AssessorSaveStatus.Saved);

    public AssessorSubmitStatus SubmitStatus { get; set; } = AssessorSubmitStatus.Submitted;

    public List<Guid> Submitted { get; } = [];

    public Task<AssessorSubmitStatus> SubmitAsync(Guid testId, CancellationToken cancellationToken = default)
    {
        Submitted.Add(testId);
        return Task.FromResult(SubmitStatus);
    }

    public List<int?> RequestedMenus { get; } = [];

    public List<Guid> RequestedItemLists { get; } = [];

    public List<(Guid SampleTestId, Guid ParticipantId)> RequestedEntries { get; } = [];

    public List<(Guid SampleTestId, Guid ParticipantId, AssessorEntrySaveRequest Request)> Saved { get; } = [];

    public Task<AssessorApiResult<AssessorEntryResponse>> GetEntryAsync(Guid sampleTestId, Guid participantId, CancellationToken cancellationToken = default)
    {
        RequestedEntries.Add((sampleTestId, participantId));
        return Task.FromResult(Entry);
    }

    public Task<AssessorSaveApiResult> SaveEntryAsync(Guid sampleTestId, Guid participantId, AssessorEntrySaveRequest request, CancellationToken cancellationToken = default)
    {
        Saved.Add((sampleTestId, participantId, request));
        return Task.FromResult(SaveResult);
    }

    public Task<AssessorApiResult<AssessorMenuResponse>> GetMenuAsync(int? year, CancellationToken cancellationToken = default)
    {
        RequestedMenus.Add(year);
        return Task.FromResult(Menu);
    }

    public Task<AssessorApiResult<AssessorItemListResponse>> GetItemListAsync(Guid testId, CancellationToken cancellationToken = default)
    {
        RequestedItemLists.Add(testId);
        return Task.FromResult(ItemList);
    }
}
