using PTL.Contracts.SystemMessage;

namespace PTL.ApiClient;

public interface ISystemMessageApiClient
{
    Task<GetImportantMessageResponse> GetImportantMessageAsync(CancellationToken cancellationToken = default);
    Task<GetMessageResponse> GetMessageAsync(CancellationToken cancellationToken = default);
}
