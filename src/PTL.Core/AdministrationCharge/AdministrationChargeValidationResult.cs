namespace PTL.Core.AdministrationCharge;

public sealed record AdministrationChargeValidationError(string Field, string Message);

public sealed record AdministrationChargeValidationResult(bool IsValid, IReadOnlyList<AdministrationChargeValidationError> Errors);
