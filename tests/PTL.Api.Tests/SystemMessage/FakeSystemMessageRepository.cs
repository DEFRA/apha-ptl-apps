using PTL.Core.SystemMessage;

namespace PTL.Api.Tests.SystemMessage;

// In-memory ISystemMessageRepository test double so SystemMessageService can be tested without a
// real database or the spgImportantMessage stored procedure.
internal sealed class FakeSystemMessageRepository : ISystemMessageRepository
{
    public string? ImportantMessage { get; set; }

    public Task<string?> GetImportantMessageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ImportantMessage);
}
