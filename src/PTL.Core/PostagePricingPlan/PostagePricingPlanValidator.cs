namespace PTL.Core.PostagePricingPlan;

// Preserves legacy PostagePricingPlan.aspx's three per-field validators (RequiredFieldValidator +
// RegularExpressionValidator "^\d+(.\d+){0,1}$") - non-negative decimal, required. Model binding
// already rejects a missing/non-numeric value before this runs, so only the non-negative rule
// needs reproducing here.
public static class PostagePricingPlanValidator
{
    public static PostagePricingPlanValidationResult Validate(decimal ukPrice, decimal euPrice, decimal nonEuPrice)
    {
        List<PostagePricingPlanValidationError> errors = [];

        if (ukPrice < 0)
        {
            errors.Add(new PostagePricingPlanValidationError("UKPrice", "A UK Price must not be negative"));
        }

        if (euPrice < 0)
        {
            errors.Add(new PostagePricingPlanValidationError("EUPrice", "A EU Price must not be negative"));
        }

        if (nonEuPrice < 0)
        {
            errors.Add(new PostagePricingPlanValidationError("NonEUPrice", "A Non EU Price must not be negative"));
        }

        return new PostagePricingPlanValidationResult(errors.Count == 0, errors);
    }
}
