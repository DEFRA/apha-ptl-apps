using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.ExternalWeb.ViewSupport;

// GOV.UK error styling and the linked message for one Results entry value, derived from its model-state key.
public sealed record ResultEntryField(bool Invalid, string? Message, string ErrorId)
{
    public string GroupClass => Invalid ? "govuk-form-group govuk-form-group--error govuk-!-margin-bottom-0" : "govuk-form-group govuk-!-margin-bottom-0";

    public string InputClass => Invalid ? "govuk-input govuk-input--error" : "govuk-input";

    public string SelectClass => Invalid ? "govuk-select govuk-select--error" : "govuk-select";

    public string? DescribedBy => Invalid ? ErrorId : null;

    public static ResultEntryField For(ModelStateDictionary modelState, string key)
    {
        var message = modelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0 ? entry.Errors[0].ErrorMessage : null;
        return new ResultEntryField(message is not null, message, TagBuilder.CreateSanitizedId(key, "_") + "-error");
    }
}
