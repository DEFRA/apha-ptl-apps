namespace PTL.Core.SystemMessage;

public interface ISystemMessageRepository
{
    /// <summary>Reads tblExtWebsiteMessage.fldImportantMessage via spgMainPageMessageBySsoId - a
    /// single admin-authored row (fldMessageId = 0) shared by every external user, not scoped to
    /// any identity.</summary>
    Task<string?> GetImportantMessageAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads tblExtWebsiteMessage.fldMessage - the general "Information" page content
    /// linked from the Important alert banner (legacy ViewInformation.aspx). Same shared row as
    /// <see cref="GetImportantMessageAsync"/>.</summary>
    Task<string?> GetMessageAsync(CancellationToken cancellationToken = default);
}
