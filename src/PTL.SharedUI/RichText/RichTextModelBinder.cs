using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PTL.SharedUI.RichText;

/// <summary>Applies to a string property that holds rich text HTML; see <see cref="RichTextTransport"/>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RichTextBinderAttribute() : ModelBinderAttribute(typeof(RichTextModelBinder));

public sealed class RichTextModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        var decoded = RichTextTransport.Decode(result.FirstValue);

        // Keep the decoded HTML as the attempted value so a redisplayed form shows the markup, not the wire format.
        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, decoded, decoded);

        if (string.IsNullOrEmpty(decoded) && bindingContext.ModelMetadata.ConvertEmptyStringToNull)
        {
            decoded = null;
        }

        bindingContext.Result = ModelBindingResult.Success(decoded);
        return Task.CompletedTask;
    }
}
