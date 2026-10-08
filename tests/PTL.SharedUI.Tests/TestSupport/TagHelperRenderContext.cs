using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PTL.SharedUI.Tests.TestSupport;

/// <summary>
/// Builds a real <see cref="ViewContext"/>/<see cref="ModelExpression"/>/<see cref="IHtmlGenerator"/>
/// triple (via a minimal MVC DI container, not a full WebApplicationFactory host) so the GOV.UK form
/// tag helpers can be exercised against the actual ASP.NET Core html-generation pipeline, exactly as
/// they run in production.
/// </summary>
internal static class TagHelperRenderContext
{
    private sealed class NullView : IView
    {
        public string Path => string.Empty;

        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    public static (ViewContext ViewContext, ModelExpression Expression, IHtmlGenerator Generator) Create<TModel>(
        TModel model,
        string propertyName,
        ModelStateDictionary? modelState = null)
        where TModel : class
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddViews();
        var provider = services.BuildServiceProvider();

        var generator = provider.GetRequiredService<IHtmlGenerator>();
        var metadataProvider = provider.GetRequiredService<IModelMetadataProvider>();

        var modelExplorer = metadataProvider.GetModelExplorerForType(typeof(TModel), model);
        var propertyExplorer = modelExplorer.GetExplorerForProperty(propertyName)
            ?? throw new InvalidOperationException($"No property named '{propertyName}' on {typeof(TModel)}.");
        var expression = new ModelExpression(propertyName, propertyExplorer);

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            httpContext,
            new RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

        var effectiveModelState = modelState ?? new ModelStateDictionary();
        var viewData = new ViewDataDictionary(metadataProvider, effectiveModelState) { Model = model };
        var tempData = new TempDataDictionary(httpContext, new FakeTempDataProvider());

        var viewContext = new ViewContext(
            actionContext,
            new NullView(),
            viewData,
            tempData,
            TextWriter.Null,
            new HtmlHelperOptions());

        return (viewContext, expression, generator);
    }
}
