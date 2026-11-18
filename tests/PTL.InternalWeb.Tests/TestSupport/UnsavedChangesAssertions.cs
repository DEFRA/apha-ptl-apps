using System.Text.RegularExpressions;

namespace PTL.InternalWeb.Tests.TestSupport;

// An Audit Trail step is armed by unsaved-changes.js from the moment it shows: leaving by a link warns, Save and Cancel do not.
internal static partial class UnsavedChangesAssertions
{
    public static void AssertAuditStepWarnsOnNavigateAway(string html)
    {
        Assert.Matches(ArmedFormPattern(), html);
        Assert.Matches(HiddenBannerPattern(), html);
        Assert.Contains("/js/unsaved-changes.js", html, StringComparison.Ordinal);
        Assert.Contains("Select <strong>Save</strong> to keep them, or <strong>Cancel</strong> to discard them.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<strong>Apply</strong>", html, StringComparison.Ordinal);
    }

    public static void AssertNotArmed(string html) =>
        Assert.DoesNotContain("data-unsaved-dirty", html, StringComparison.Ordinal);

    [GeneratedRegex("<form [^>]*data-unsaved-dirty")]
    private static partial Regex ArmedFormPattern();

    [GeneratedRegex("id=\"unsaved-changes\"[^>]*hidden")]
    private static partial Regex HiddenBannerPattern();
}
