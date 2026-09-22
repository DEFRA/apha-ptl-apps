using Microsoft.AspNetCore.Mvc;

namespace PTL.InternalWeb.Features.Menu;

// Lightweight landing pages for top-level PTLIMS menu sections that group several admin
// screens - mirrors the legacy Contracts Admin/MenuContracts.aspx and Scheme Admin/MenuSchemes.aspx
// pages, which likewise only show a heading, a short description, and rely on the left nav
// (SideNavigationProvider) for the actual section contents.
public class MenuController : Controller
{
    public IActionResult ManageContracts() => View();

    public IActionResult ManageSchemes() => View();
}
