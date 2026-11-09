using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PTL.InternalWeb.ModelBinding;

// Binds the single DD/MM/YYYY date-entry textbox. The default DateTime binder parses with the
// server's culture, so on an invariant-culture host "08/04/2027" would silently become 4 August;
// this binder always reads day-first, whatever the host culture. Apply with
// [ModelBinder(typeof(UkDateModelBinder))] to a DateTime? property.
public sealed class UkDateModelBinder : IModelBinder
{
    public const string DisplayFormat = "dd'/'MM'/'yyyy";

    private static readonly string[] AcceptedFormats = ["dd/MM/yyyy", "d/M/yyyy"];

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;
        var value = bindingContext.ValueProvider.GetValue(modelName);
        if (value == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, value);

        var text = value.FirstValue?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (DateTime.TryParseExact(text, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            modelName,
            $"{bindingContext.ModelMetadata.GetDisplayName()} must be a real date in the format DD/MM/YYYY, for example 08/04/2027.");
        bindingContext.Result = ModelBindingResult.Failed();
        return Task.CompletedTask;
    }
}
