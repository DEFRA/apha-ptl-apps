using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.InternalWeb.Tests.Integration;

// ITHC-004: the self-hosted TinyMCE (wwwroot/lib/tinymce) and the editor configuration that loads it.
public partial class TinyMceAssetTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ExpectedVersion = "8.11.0";

    [Theory]
    [InlineData("/lib/tinymce/tinymce.min.js")]
    [InlineData("/lib/tinymce/models/dom/model.min.js")]
    [InlineData("/lib/tinymce/themes/silver/theme.min.js")]
    [InlineData("/lib/tinymce/icons/default/icons.min.js")]
    [InlineData("/lib/tinymce/skins/ui/oxide/skin.min.css")]
    [InlineData("/lib/tinymce/skins/content/default/content.min.css")]
    [InlineData("/lib/tinymce/plugins/lists/plugin.min.js")]
    [InlineData("/lib/tinymce/plugins/save/plugin.min.js")]
    [InlineData("/lib/tinymce/plugins/charmap/plugin.min.js")]
    public async Task EveryFileTheEditorLoads_IsServed(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task VendoredTinyMce_IsThePatchedVersion()
    {
        Assert.Equal(ExpectedVersion, await VendoredVersionAsync());
    }

    [Fact]
    public async Task EditorConfiguration_IsUnchangedAndVersionsTheFilesTinyMceLoadsItself()
    {
        var script = await factory.CreateClient().GetStringAsync("/js/ptl-rich-text-editor.js");

        Assert.Contains("license_key: 'gpl'", script, StringComparison.Ordinal);
        Assert.Contains("plugins: 'lists save charmap'", script, StringComparison.Ordinal);
        Assert.Contains("entity_encoding: 'named'", script, StringComparison.Ordinal);
        Assert.Contains("encoding: 'xml'", script, StringComparison.Ordinal);
        Assert.Contains("editor.on('SaveContent'", script, StringComparison.Ordinal);
        Assert.Contains("editor.on('BeforeSetContent'", script, StringComparison.Ordinal);
        Assert.Contains($"cache_suffix: '?v={await VendoredVersionAsync()}'", script, StringComparison.Ordinal);
    }

    private async Task<string> VendoredVersionAsync()
    {
        var banner = await factory.CreateClient().GetStringAsync("/lib/tinymce/tinymce.js");
        return VersionBanner().Match(banner).Groups[1].Value;
    }

    [GeneratedRegex(@"TinyMCE version (\d+\.\d+\.\d+)")]
    private static partial Regex VersionBanner();
}
