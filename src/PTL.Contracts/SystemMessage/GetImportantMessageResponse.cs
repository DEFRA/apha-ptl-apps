namespace PTL.Contracts.SystemMessage;

/// <summary>Response body for <c>GET /api/system-messages/important</c>.</summary>
public sealed record GetImportantMessageResponse(string? ImportantMessage);
