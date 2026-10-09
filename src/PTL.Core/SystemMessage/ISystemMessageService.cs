namespace PTL.Core.SystemMessage;

public interface ISystemMessageService
{
    Task<string?> GetImportantMessageAsync(CancellationToken cancellationToken = default);
    Task<string?> GetMessageAsync(CancellationToken cancellationToken = default);
}
