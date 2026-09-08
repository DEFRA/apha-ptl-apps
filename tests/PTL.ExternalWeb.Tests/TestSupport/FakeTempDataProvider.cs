using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PTL.ExternalWeb.Tests.TestSupport;

// In-memory ITempDataProvider so controllers using TempData can be unit tested without a real session/cookie store.
internal sealed class FakeTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
    }
}
