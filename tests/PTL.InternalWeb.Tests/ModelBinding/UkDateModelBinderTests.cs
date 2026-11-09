using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using PTL.InternalWeb.ModelBinding;

namespace PTL.InternalWeb.Tests.ModelBinding;

public class UkDateModelBinderTests
{
    private sealed class Row
    {
        public DateTime? Date { get; set; }
    }

    private static async Task<(ModelBindingContext Context, ModelStateDictionary State)> BindAsync(string? posted)
    {
        var values = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        if (posted is not null)
        {
            values["Date"] = posted;
        }

        var state = new ModelStateDictionary();
        var metadata = new EmptyModelMetadataProvider().GetMetadataForProperty(typeof(Row), nameof(Row.Date));
        var context = new DefaultModelBindingContext
        {
            ModelName = "Date",
            ModelMetadata = metadata,
            ModelState = state,
            ValueProvider = new FormValueProvider(BindingSource.Form, new FormCollection(values), CultureInfo.InvariantCulture)
        };

        await new UkDateModelBinder().BindModelAsync(context);
        return (context, state);
    }

    [Theory]
    [InlineData("08/04/2027", 2027, 4, 8)]
    [InlineData("8/4/2027", 2027, 4, 8)]
    [InlineData("31/12/2026", 2026, 12, 31)]
    [InlineData("  01/01/2027  ", 2027, 1, 1)]
    public async Task Bind_DayFirstText_ParsesToThatDateWhateverTheHostCulture(string posted, int year, int month, int day)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var (context, state) = await BindAsync(posted);

            Assert.True(context.Result.IsModelSet);
            Assert.Equal(new DateTime(year, month, day), context.Result.Model);
            Assert.Equal(0, state.ErrorCount);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Bind_BlankText_BindsNullSoRequiredRulesStillApply(string posted)
    {
        var (context, state) = await BindAsync(posted);

        Assert.True(context.Result.IsModelSet);
        Assert.Null(context.Result.Model);
        Assert.Equal(0, state.ErrorCount);
    }

    [Fact]
    public async Task Bind_NoPostedValue_LeavesTheModelUnset()
    {
        var (context, state) = await BindAsync(null);

        Assert.False(context.Result.IsModelSet);
        Assert.True(state.IsValid);
    }

    [Theory]
    [InlineData("2027-04-08")]
    [InlineData("31/02/2027")]
    [InlineData("13/13/2027")]
    [InlineData("08/04/27")]
    [InlineData("08 04 2027")]
    [InlineData("tomorrow")]
    public async Task Bind_NotARealDayFirstDate_AddsFormatErrorAndKeepsTheTypedValue(string posted)
    {
        var (context, state) = await BindAsync(posted);

        Assert.False(context.Result.IsModelSet);
        var entry = state["Date"]!;
        Assert.Equal(posted, entry.AttemptedValue);
        var error = Assert.Single(entry.Errors);
        Assert.Contains("must be a real date in the format DD/MM/YYYY, for example 08/04/2027.", error.ErrorMessage, StringComparison.Ordinal);
    }
}
