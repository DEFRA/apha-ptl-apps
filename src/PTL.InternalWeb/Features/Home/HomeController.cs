using PTL.ApiClient;

namespace PTL.InternalWeb.Features.Home;

public class HomeController(IApiClient apiClient) : HomeControllerBase(apiClient);
