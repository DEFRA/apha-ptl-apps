using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PTL.ExternalWeb.Infrastructure;

public interface IRazorViewRenderer
{
    Task<string> RenderAsync(ActionContext actionContext, string viewPath, object model);
}

public sealed class RazorViewRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider serviceProvider) : IRazorViewRenderer
{
    public async Task<string> RenderAsync(ActionContext actionContext, string viewPath, object model)
    {
        ArgumentNullException.ThrowIfNull(actionContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewPath);

        var view = FindView(actionContext, viewPath);

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };

        var viewContext = new ViewContext(
            actionContext,
            view,
            viewData,
            new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await view.RenderAsync(viewContext);
        return writer.ToString();
    }

    private IView FindView(ActionContext actionContext, string viewPath)
    {
        var direct = viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);
        if (direct.Success)
        {
            return direct.View;
        }

        var fromAction = viewEngine.FindView(actionContext, viewPath, isMainPage: true);
        if (fromAction.Success)
        {
            return fromAction.View;
        }

        var searched = direct.SearchedLocations.Concat(fromAction.SearchedLocations);
        throw new InvalidOperationException($"Could not find view '{viewPath}'. Searched: {string.Join(", ", searched)}");
    }
}
