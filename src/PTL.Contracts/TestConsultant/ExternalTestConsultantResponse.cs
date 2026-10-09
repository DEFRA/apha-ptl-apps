namespace PTL.Contracts.TestConsultant;

public sealed record ExternalTestConsultantResponse(
    Guid ExternalTestConsultantId,
    string Name,
    string Department,
    string Email,
    bool IsInactive,
    DateTime? InactiveDate,
    bool HasLogin);
