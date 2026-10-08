namespace PTL.Core.SystemMessage;

public interface ISystemMessageRepository
{
    /// <summary>Reads tblExtWebsiteMessage.fldImportantMessage via spgImportantMessage - a single
    /// admin-authored row (fldMessageId = 0) shared by every external user, not scoped to any
    /// identity.</summary>
    Task<string?> GetImportantMessageAsync(CancellationToken cancellationToken = default);
}
