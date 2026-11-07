using System.Net;
using System.Text.RegularExpressions;

namespace PTL.InternalWeb.Tests.Integration;

// One start tag of a rendered page: its attributes (names are case-insensitive) and, for an element with content, the markup between its tags.
internal sealed record HtmlTag(string Name, IReadOnlyDictionary<string, string> Attributes, string Inner)
{
    public string? this[string attribute] => Attributes.TryGetValue(attribute, out var value) ? value : null;

    public bool Has(string attribute) => Attributes.ContainsKey(attribute);

    // The text a user reads: tags dropped, entities decoded, whitespace collapsed. A textarea's value is kept as typed.
    public string Text => Name == "textarea"
        ? WebUtility.HtmlDecode(Inner.StartsWith("\r\n", StringComparison.Ordinal) ? Inner[2..] : Inner.StartsWith('\n') ? Inner[1..] : Inner)
        : WebUtility.HtmlDecode(SchemeFormPage.WhitespacePattern().Replace(SchemeFormPage.TagPattern().Replace(Inner, " "), " ")).Trim();
}

// The form controls and commands of a rendered Scheme page. No HTML parser package is referenced by this project, so
// start tags are tokenised with a regex (as BreadcrumbHtml does) - enough for assertions about attributes and text.
internal sealed partial class SchemeFormPage
{
    private static readonly HashSet<string> ElementsWithContent =
        ["a", "button", "dd", "dt", "h1", "h2", "h3", "h4", "label", "legend", "option", "p", "select", "textarea", "th"];

    private readonly IReadOnlyList<HtmlTag> _tags;

    private SchemeFormPage(string main, IReadOnlyList<HtmlTag> tags)
    {
        Main = main;
        _tags = tags;
    }

    // The page's <main> markup with scripts and comments removed.
    public string Main { get; }

    public IEnumerable<HtmlTag> Controls => _tags.Where(t => t.Name is "input" or "select" or "textarea");

    // What the form submits under a name: a disabled control, an unticked checkbox and an unnamed control submit nothing.
    public IEnumerable<HtmlTag> Posting(string name) => Controls.Where(c =>
        c["name"] == name && !c.Has("disabled") && (c["type"] is not ("checkbox" or "radio") || c.Has("checked")));

    // Every control carrying a name, whether or not it submits.
    public IEnumerable<HtmlTag> Named(string name) => Controls.Where(c => c["name"] == name);

    public IEnumerable<HtmlTag> Tags(string name) => _tags.Where(t => t.Name == name);

    public HtmlTag ById(string id) => Assert.Single(_tags, t => t["id"] == id);

    public IReadOnlyList<string> Commands => [.. Tags("button").Where(b => b["name"] == "testCommand").Select(b => b["value"] ?? string.Empty)];

    public IReadOnlyList<string> ButtonTexts => [.. Tags("button").Select(b => b.Text)];

    // The label and value of each row of a view-only summary list, in page order.
    public IReadOnlyList<(string Label, string Value)> SummaryRows =>
        [.. Tags("dt").Zip(Tags("dd"), (term, detail) => (term.Text, detail.Text))];

    public static SchemeFormPage Parse(string html)
    {
        var main = MainPattern().Match(html).Value;
        Assert.True(main.Length > 0, "The page has no <main> content.");
        main = ScriptsAndCommentsPattern().Replace(main, string.Empty);

        return new SchemeFormPage(main, Tokenise(main));
    }

    // The values of the options a select will submit.
    public static IReadOnlyList<string> SelectedValues(HtmlTag select) =>
        [.. Tokenise(select.Inner).Where(t => t.Name == "option" && t.Has("selected")).Select(t => t["value"] ?? t.Text)];

    public static IReadOnlyList<string> OptionTexts(HtmlTag select) =>
        [.. Tokenise(select.Inner).Where(t => t.Name == "option").Select(t => t.Text)];

    // What a browser submits for the page's form: each enabled, named control in page order - a checkbox only when ticked, a select's
    // chosen option (the first when none is marked) and an empty string for a control with no value.
    public List<KeyValuePair<string, string>> Submission()
    {
        var fields = new List<KeyValuePair<string, string>>();
        foreach (var control in Controls.Where(c => !string.IsNullOrEmpty(c["name"]) && !c.Has("disabled")))
        {
            var name = control["name"]!;
            if (control.Name == "textarea")
            {
                fields.Add(new(name, control.Text));
            }
            else if (control.Name == "select")
            {
                fields.AddRange(ChosenValues(control).Select(value => new KeyValuePair<string, string>(name, value)));
            }
            else if (control["type"] is "checkbox" or "radio")
            {
                if (control.Has("checked"))
                {
                    fields.Add(new(name, control["value"] ?? "on"));
                }
            }
            else
            {
                fields.Add(new(name, control["value"] ?? string.Empty));
            }
        }

        return fields;
    }

    private static List<string> ChosenValues(HtmlTag select)
    {
        var options = Tokenise(select.Inner).Where(t => t.Name == "option").ToList();
        var chosen = options.Where(o => o.Has("selected")).ToList();
        if (chosen.Count == 0 && !select.Has("multiple") && options.Count > 0)
        {
            chosen.Add(options[0]);
        }

        return [.. chosen.Select(o => o["value"] ?? o.Text)];
    }

    // Locked text: one read-only text input under the field's own name, carrying the value.
    public void AssertLockedText(string field, string? expected)
    {
        var input = Assert.Single(Named(field));
        Assert.Equal("input", input.Name);
        Assert.Equal("text", input["type"]);
        Assert.True(input.Has("readonly"), $"{field} should be read-only.");
        Assert.False(input.Has("disabled"), $"{field} must not be disabled, or it would not post its value.");
        Assert.Equal(expected ?? string.Empty, input["value"] ?? string.Empty);
    }

    public void AssertLockedTextArea(string field, string? expected)
    {
        var area = Assert.Single(Named(field));
        Assert.Equal("textarea", area.Name);
        Assert.True(area.Has("readonly"), $"{field} should be read-only.");
        Assert.False(area.Has("disabled"), $"{field} must not be disabled, or it would not post its value.");
        Assert.Equal(expected ?? string.Empty, area.Text);
    }

    // Locked select: a read-only text input without a name showing the chosen option's text, and a hidden input carrying the value.
    public void AssertLockedSelect(string field, string? expectedValue, string expectedText)
    {
        var hidden = Assert.Single(Named(field));
        Assert.Equal("input", hidden.Name);
        Assert.Equal("hidden", hidden["type"]);
        Assert.Equal(expectedValue ?? string.Empty, hidden["value"] ?? string.Empty);

        var shown = ById(field);
        Assert.Equal("input", shown.Name);
        Assert.Equal("text", shown["type"]);
        Assert.True(shown.Has("readonly"), $"{field} should be read-only.");
        Assert.False(shown.Has("name"), $"The visible {field} text must not post under the field's name.");
        Assert.Equal(expectedText, shown["value"] ?? string.Empty);
        Assert.DoesNotContain(Tags("select"), s => s["name"] == field);
    }

    // Locked checkbox: a disabled checkbox without a name, and a hidden input carrying "true" or "false".
    public void AssertLockedCheckbox(string field, bool expected)
    {
        var hidden = Assert.Single(Named(field));
        Assert.Equal("input", hidden.Name);
        Assert.Equal("hidden", hidden["type"]);
        Assert.Equal(expected ? "true" : "false", hidden["value"]);

        var box = ById(field);
        Assert.Equal("checkbox", box["type"]);
        Assert.True(box.Has("disabled"), $"{field} should be a disabled checkbox.");
        Assert.False(box.Has("name"), $"The visible {field} checkbox must not post under the field's name.");
        Assert.Equal(expected, box.Has("checked"));
    }

    // A control the user can change: named, and neither read-only, disabled nor hidden.
    public HtmlTag AssertEditable(string field)
    {
        var editable = Controls.Where(c => c["name"] == field && c["type"] != "hidden").ToList();
        var control = Assert.Single(editable);
        Assert.False(control.Has("readonly"), $"{field} should be editable, not read-only.");
        Assert.False(control.Has("disabled"), $"{field} should be editable, not disabled.");
        return control;
    }

    private static List<HtmlTag> Tokenise(string html)
    {
        var tags = new List<HtmlTag>();
        foreach (Match match in StartTagPattern().Matches(html))
        {
            var name = match.Groups["name"].Value.ToLowerInvariant();
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attribute in AttributePattern().Matches(match.Groups["attributes"].Value))
            {
                attributes[attribute.Groups["name"].Value] = WebUtility.HtmlDecode(attribute.Groups["value"].Value);
            }

            var inner = string.Empty;
            if (ElementsWithContent.Contains(name))
            {
                var start = match.Index + match.Length;
                var end = html.IndexOf($"</{name}>", start, StringComparison.OrdinalIgnoreCase);
                inner = end < 0 ? string.Empty : html[start..end];
            }

            tags.Add(new HtmlTag(name, attributes, inner));
        }

        return tags;
    }

    [GeneratedRegex("<main.*?</main>", RegexOptions.Singleline, 5000)]
    private static partial Regex MainPattern();

    [GeneratedRegex("<script\\b.*?</script>|<!--.*?-->", RegexOptions.Singleline | RegexOptions.IgnoreCase, 5000)]
    private static partial Regex ScriptsAndCommentsPattern();

    // A quoted attribute value may contain ">", so quoted strings are consumed whole.
    [GeneratedRegex("<(?<name>[A-Za-z][A-Za-z0-9-]*)(?<attributes>(?:\"[^\"]*\"|'[^']*'|[^>\"'])*)>", RegexOptions.None, 5000)]
    private static partial Regex StartTagPattern();

    [GeneratedRegex("(?<name>[^\\s\"'<>/=]+)(?:\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s\"'=<>`]+)))?", RegexOptions.None, 5000)]
    private static partial Regex AttributePattern();

    [GeneratedRegex("<[^>]+>", RegexOptions.None, 5000)]
    internal static partial Regex TagPattern();

    [GeneratedRegex("\\s+", RegexOptions.None, 5000)]
    internal static partial Regex WhitespacePattern();
}
