namespace PTL.InternalWeb.Navigation;

// Several breadcrumb ancestors are parameterised routes (Participants and Contracts need a
// customerId; Contract Items needs a contractId). A child page only sometimes carries those ids in
// its own query string or route values - e.g. /Contract/Details/{id} knows the contract but not the
// customer - so pages publish them here for _Breadcrumbs.cshtml to build working ancestor links.
// Where a value is absent the crumb renders as plain text rather than a link to a broken route.
public static class BreadcrumbRouteValues
{
    public const string CustomerId = "BreadcrumbCustomerId";
    public const string ContractId = "BreadcrumbContractId";

    // Ambient value names a SideNavigationItem can reference via LinkRouteValueKey.
    public const string CustomerIdKey = "customerId";
    public const string ContractIdKey = "contractId";
}
