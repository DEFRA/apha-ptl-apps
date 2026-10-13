namespace PTL.Core.PostagePricingPlan;

public sealed record PostagePricingPlanValidationError(string Field, string Message);

public sealed record PostagePricingPlanValidationResult(bool IsValid, IReadOnlyList<PostagePricingPlanValidationError> Errors);
