namespace PTL.Core.SystemMessage;

public interface ISystemMessageService
{
    Task<string?> GetImportantMessageAsync(CancellationToken cancellationToken = default);
}
