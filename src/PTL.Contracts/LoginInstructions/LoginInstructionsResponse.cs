namespace PTL.Contracts.LoginInstructions;

// Success=false carries the reason (no email address saved, or GOV.UK Notify did not accept the email); an unknown record is a 404.
public sealed record LoginInstructionsResponse(bool Success, string? Message = null);
