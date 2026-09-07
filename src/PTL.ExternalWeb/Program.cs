using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
// Also enable Razor Pages (some projects in the solution use Razor Pages)
builder.Services.AddRazorPages();

// Allow views to be located under Features/{Controller}/Views and Features/Shared
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Insert(0, "/Features/{1}/Views/{0}.cshtml");
    options.ViewLocationFormats.Insert(1, "/Features/Shared/{0}.cshtml");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Serve static files from wwwroot
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

// Ensure Razor Pages are available if any exist in the project
app.MapRazorPages();

app.Run();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.ExternalWeb.Tests.
public partial class Program { }
