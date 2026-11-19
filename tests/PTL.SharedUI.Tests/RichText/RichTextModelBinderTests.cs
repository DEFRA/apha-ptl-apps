using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using PTL.SharedUI.RichText;

namespace PTL.SharedUI.Tests.RichText;

public class RichTextModelBinderTests
{
    private const string Field = "Comments";

    private static string Wire(string html) => RichTextTransport.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(html));

    private static async Task<(ModelBindingResult Result, ModelStateDictionary State)> BindAsync(string? posted, bool include = true)
    {
        var values = new Dictionary<string, StringValues>();
        if (include)
        {
            values[Field] = posted;
        }

        var state = new ModelStateDictionary();
        var context = new DefaultModelBindingContext
        {
            ModelName = Field,
            ModelState = state,
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(string)),
            ValueProvider = new FormValueProvider(BindingSource.Form, new FormCollection(values), CultureInfo.InvariantCulture),
        };

        await new RichTextModelBinder().BindModelAsync(context);
        return (context.Result, state);
    }

    [Fact]
    public async Task Bind_EncodedValue_BindsTheHtmlAndKeepsItAsTheAttemptedValue()
    {
        const string html = "<p>Hello <strong>world</strong></p>";

        var (result, state) = await BindAsync(Wire(html));

        Assert.True(result.IsModelSet);
        Assert.Equal(html, result.Model);
        Assert.Equal(html, state[Field]!.AttemptedValue);
    }

    [Fact]
    public async Task Bind_PlainValue_IsBoundAsPosted()
    {
        var (result, _) = await BindAsync("<p>no script</p>");

        Assert.Equal("<p>no script</p>", result.Model);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ptl-b64:")]
    public async Task Bind_EmptyValue_BindsNullLikeTheDefaultStringBinder(string posted)
    {
        var (result, _) = await BindAsync(posted);

        Assert.True(result.IsModelSet);
        Assert.Null(result.Model);
    }

    [Fact]
    public async Task Bind_FieldNotPosted_LeavesTheModelUnset()
    {
        var (result, state) = await BindAsync(null, include: false);

        Assert.False(result.IsModelSet);
        Assert.Empty(state);
    }
}
