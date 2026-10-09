namespace PTL.Core.SystemMessage;

public sealed class SystemMessageService(ISystemMessageRepository systemMessageRepository) : ISystemMessageService
{
    public Task<string?> GetImportantMessageAsync(CancellationToken cancellationToken = default) =>
        systemMessageRepository.GetImportantMessageAsync(cancellationToken);

    public Task<string?> GetMessageAsync(CancellationToken cancellationToken = default) =>
        systemMessageRepository.GetMessageAsync(cancellationToken);
}
