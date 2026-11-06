using Microsoft.AspNetCore.Mvc;

namespace PTL.ExternalWeb.ViewSupport;

// Legacy Footer.ascx linked Help.aspx#contextHelp{PageTitle}; the anchors here are the Help page's section ids.
public static class HelpLink
{
    private static readonly Dictionary<string, string> Anchors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Home/Index"] = "contextHelpHome",
        ["Distribution/Current"] = "contextHelpCurrentdistributions",
        ["Distribution/Entry"] = "contextHelpResultsentry",
        ["Distribution/Previous"] = "contextHelpPreviousdistributions",
        ["Distribution/Results"] = "contextHelpViewresults",
        ["Comments/Index"] = "contextHelpDistributions",
        ["Tabulation/Index"] = "contextHelpReports",
        ["Tabulation/Report"] = "contextHelpViewreports",
        ["Tabulation/AssessmentResults"] = "contextHelpViewreports",
    };

    public static string Href(IUrlHelper url, string controller, string action)
    {
        var path = url.Action("Index", "Help") ?? "/Help";
        return Anchors.TryGetValue($"{controller}/{action}", out var anchor) ? $"{path}#{anchor}" : path;
    }
}
