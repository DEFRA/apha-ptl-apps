namespace PTL.Contracts.SystemMessage;

/// <summary>Response body for <c>GET /api/system-messages/important</c>.</summary>
public sealed record GetImportantMessageResponse(string? ImportantMessage);

/// <summary>Response body for <c>GET /api/system-messages/general</c>.</summary>
public sealed record GetMessageResponse(string? Message);
