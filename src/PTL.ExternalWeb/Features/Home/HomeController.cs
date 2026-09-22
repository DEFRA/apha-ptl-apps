using PTL.ApiClient;

namespace PTL.ExternalWeb.Features.Home;

public class HomeController(IApiClient apiClient) : HomeControllerBase(apiClient);
