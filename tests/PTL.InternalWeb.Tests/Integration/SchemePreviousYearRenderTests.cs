using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders the closed-year (Previous Year) Scheme form through the real view engine and checks the other Scheme pages are unchanged.
public class SchemePreviousYearRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    // What a Previous Year scheme locks on the Details, Test Consultants, Assessors and Results Tabulations tabs.
    private static readonly string[] LockedFields =
    [
        "Identifier", "Name", "ScheduleId", "ScheduleCodeId", "WeekNumber", "DayOfWeekId", "NumberOfSamples", "SampleOrigin",
        "Subcontractor", "Deadline", "SamplePackingInstructions", "Postage", "Prices[0].Price", "Prices[1].Price",
        "DistributionMonthApr", "DistributionMonthMay", "DistributionMonthJun", "DistributionMonthJul", "DistributionMonthAug",
        "DistributionMonthSep", "DistributionMonthOct", "DistributionMonthNov", "DistributionMonthDec", "DistributionMonthJan",
        "DistributionMonthFeb", "DistributionMonthMar", "DistributionAsAvailable", "CombinedPackaging", "CommentsRequired", "Pilot",
        "LimitedSampleAvailability", "Accredited", "NoVLALabs", "ComerciallyAvailable", "StoreRatings",
        "TestConsultant1", "TestConsultant2", "TestConsultant3", "Assessor1", "Assessor2", "Assessor3", "Assessor4",
    ];

    // The first item of a list has no Up, the last no Down, a lone item neither and an empty list no buttons.
    private static readonly string[] ResultAndMethodReordering =
    [
        "move-result-down:0:0",
        "move-result-up:0:1", "move-result-down:0:1",
        "move-result-up:0:2",
        "move-method-down:0:0",
        "move-method-up:0:1",
    ];

    private static readonly string[] CategoryAndCriterionActions =
    [
        "move-category-down:0:0", "remove-category:0:0",
        "move-criterion-down:0:0:0",
        "move-criterion-up:0:0:1", "move-criterion-down:0:0:1",
        "move-criterion-up:0:0:2",
        "move-category-up:0:1", "remove-category:0:1",
        "remove-category:1:0",
    ];

    private static readonly string[] AddAndRemoveActions =
    [
        "add-test", "remove-test", "move-test-up", "move-test-down", "add-result", "remove-result", "add-method", "remove-method",
        "add-category", "add-criterion", "remove-criterion",
    ];

    private static readonly (string Field, Guid? Id, string Text)[] ConsultantStaff =
    [
        ("TestConsultant1", SchemeFormTestData.TestConsultant1, "Ada Internal"),
        ("TestConsultant2", SchemeFormTestData.TestConsultant2, "Ben Deputy"),
        ("TestConsultant3", SchemeFormTestData.TestConsultant3, "Cleo External"),
        ("Assessor1", null, string.Empty),
        ("Assessor2", null, string.Empty),
        ("Assessor3", null, string.Empty),
        ("Assessor4", null, string.Empty),
    ];

    private static readonly (string Field, Guid? Id, string Text)[] AssessorStaff =
    [
        ("TestConsultant1", null, string.Empty),
        ("TestConsultant2", null, string.Empty),
        ("TestConsultant3", null, string.Empty),
        ("Assessor1", SchemeFormTestData.Assessors[0], "Alice Assessor"),
        ("Assessor2", SchemeFormTestData.Assessors[1], "Bruno Assessor"),
        ("Assessor3", SchemeFormTestData.Assessors[2], "Chloe Assessor"),
        ("Assessor4", SchemeFormTestData.Assessors[3], "Dmitri Assessor"),
    ];

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeSchemeApiClient _schemes = new();

    public SchemePreviousYearRenderTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(_schemes);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(SchemeFormTestData.Lookups());
            }));

    private static async Task<SchemeFormPage> OpenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return SchemeFormPage.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<SchemeFormPage> PageAsync(string path, SchemeResponse? scheme)
    {
        _schemes.SchemeResponse = scheme;
        return await OpenAsync(_factory.CreateClient(), path);
    }

    private Task<SchemeFormPage> PreviousYearPageAsync(string route, bool requiresAssessment) =>
        PageAsync($"/Scheme/{route}/{SchemeFormTestData.SchemeId}", SchemeFormTestData.Scheme(requiresAssessment, isReadOnly: true));

    private Task<SchemeFormPage> OpenYearPageAsync(string route, bool requiresAssessment) =>
        PageAsync($"/Scheme/{route}/{SchemeFormTestData.SchemeId}", SchemeFormTestData.Scheme(requiresAssessment, isReadOnly: false));

    // Posts what a browser would send for the form as rendered, after any change the test makes to it.
    private static async Task<HttpResponseMessage> ApplyAsync(
        HttpClient client, SchemeFormPage page, Action<List<KeyValuePair<string, string>>>? change = null, string? command = null)
    {
        var fields = page.Submission();
        change?.Invoke(fields);
        if (command is not null)
        {
            fields.Add(new("testCommand", command));
        }

        return await client.PostAsync(Assert.Single(page.Tags("form"))["action"], new FormUrlEncodedContent(fields));
    }

    private static void SetField(List<KeyValuePair<string, string>> fields, string name, string value)
    {
        var index = fields.FindIndex(f => f.Key == name);

        Assert.True(index >= 0, $"The form posts no {name}.");
        fields[index] = new(name, value);
    }

    private static async Task<string> RedisplayedErrorsAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var errors = html.Contains("<main", StringComparison.Ordinal)
            ? SchemeFormPage.Parse(html).Tags("a")
                .Where(a => a["href"] is { } href && href.StartsWith('#') && !href.StartsWith("#tab-", StringComparison.Ordinal))
                .Select(a => a.Text)
            : [];

        return $"Expected a redirect, got {(int)response.StatusCode}: {string.Join("; ", errors)}";
    }

    // The editable Create/Edit form: legacy Scheme.aspx offers Save, Apply and Cancel, and no Back.
    private static void AssertSaveApplyAndCancel(SchemeFormPage page)
    {
        Assert.Equal("submit", Assert.Single(page.Tags("button"), b => b.Text == "Save")["type"]);
        Assert.Equal("submit", Assert.Single(page.Tags("button"), b => b.Text == "Apply")["type"]);
        Assert.Equal("/Scheme/Index", Assert.Single(page.Tags("a"), a => a.Text == "Cancel")["href"]);
        Assert.DoesNotContain(page.Tags("a"), a => a.Text == "Back");
    }

    // Every control and command of the page except the antiforgery token, which is different on every response.
    private static List<string> Fingerprint(SchemeFormPage page) =>
    [
        .. page.Controls
            .Where(c => c["name"] != "__RequestVerificationToken")
            .Select(c => $"{c.Name} id={c["id"]} name={c["name"]} type={c["type"]} value={c["value"]} readonly={c.Has("readonly")} "
                + $"disabled={c.Has("disabled")} checked={c.Has("checked")} text={(c.Name == "textarea" ? c.Text : null)}"),
        .. page.Commands,
        .. page.ButtonTexts,
        .. page.Tags("a").Select(a => $"{a.Text} -> {a["href"]}"),
    ];

    private static IEnumerable<Guid> TestIds(IEnumerable<SchemeTestResponse> tests, bool withCategories) =>
        tests.SelectMany(t => new[] { t.TestId, t.TestTypeId }
            .Concat(t.ResultItems.SelectMany(i => new[] { i.ItemId, i.ItemTypeId }))
            .Concat(t.MethodItems.SelectMany(i => new[] { i.ItemId, i.ItemTypeId }))
            .Concat(withCategories
                ? t.Categories.SelectMany(c => new[] { c.CategoryItemId, c.CategoryItemTypeId }.Concat(c.Criteria.SelectMany(i => new[] { i.ItemId, i.ItemTypeId })))
                : []));

    private static IEnumerable<Guid> TestIds(IEnumerable<SchemeTestRequest> tests) =>
        tests.SelectMany(t => new[] { t.TestId, t.TestTypeId }
            .Concat(t.ResultItems.SelectMany(i => new[] { i.ItemId, i.ItemTypeId }))
            .Concat(t.MethodItems.SelectMany(i => new[] { i.ItemId, i.ItemTypeId }))
            .Concat(t.Categories.SelectMany(c => new[] { c.CategoryItemId, c.CategoryItemTypeId }.Concat(c.Criteria.SelectMany(i => new[] { i.ItemId, i.ItemTypeId })))));

    // What a page lets the user do, without the values that legitimately differ between the two schemes (year, ids in tokens).
    private static List<string> Actions(SchemeFormPage page) =>
    [
        .. page.Tags("h1").Select(h => $"h1 {h.Text}"),
        .. page.Tags("form").Select(f => $"form {f["method"]} {f["action"]}"),
        .. page.Tags("a").Select(a => $"a {a.Text} -> {a["href"]}"),
        .. page.ButtonTexts.Select(text => $"button {text}"),
        .. page.Controls.Select(c => $"{c.Name} {c["name"]} {c["type"]}"),
        .. page.SummaryRows.Select(row => $"row {row.Label}"),
    ];

    // ---- The Previous Year flow: Details (view page) -> Edit scheme -> the limited form with Apply and Back ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_Details_RendersTheViewPageWithOnlyAnEditSchemeLink(bool requiresAssessment)
    {
        var page = await PreviousYearPageAsync("Details", requiresAssessment);

        Assert.Equal($"{SchemeFormTestData.Identifier} - {SchemeFormTestData.Name}", Assert.Single(page.Tags("h1")).Text);
        Assert.Equal($"/Scheme/Edit/{SchemeFormTestData.SchemeId}", Assert.Single(page.Tags("a"), a => a.Text == "Edit scheme")["href"]);

        // A view, not a form: nothing posts to Apply and there is nothing to type into.
        Assert.Empty(page.Tags("form"));
        Assert.Empty(page.Tags("button"));
        Assert.DoesNotContain(page.Tags("a"), a => a.Text is "Apply" or "Back" or "Save" or "Cancel");
        Assert.DoesNotContain("/Scheme/Apply", page.Main, StringComparison.Ordinal);
        Assert.All(page.Controls, c => Assert.Equal("hidden", c["type"]));
        Assert.DoesNotContain("Read only (closed year)", page.Main, StringComparison.Ordinal);

        var summary = page.SummaryRows;
        Assert.Contains(("Identifier", SchemeFormTestData.Identifier), summary);
        Assert.Contains(("Name", SchemeFormTestData.Name), summary);
        Assert.Contains(("Year", "2025/26"), summary);
        Assert.Contains(("Schedule", "Quarterly Schedule"), summary);
        Assert.Contains(("Requires Assessment", requiresAssessment ? "Yes" : "No"), summary);
        Assert.Contains(requiresAssessment ? ("Primary Assessor", "Alice Assessor") : ("Primary Test Consultant", "Ada Internal"), summary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_Details_OffersTheSameActionsAsDetailsOfASchemeThatIsNotReadOnly(bool requiresAssessment)
    {
        var closedYear = await PreviousYearPageAsync("Details", requiresAssessment);
        var openYear = await OpenYearPageAsync("Details", requiresAssessment);

        Assert.Equal(Actions(openYear), Actions(closedYear));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_DetailsDiffersFromEdit_ViewPageVersusLimitedForm(bool requiresAssessment)
    {
        var details = await PreviousYearPageAsync("Details", requiresAssessment);
        var edit = await PreviousYearPageAsync("Edit", requiresAssessment);

        Assert.Equal($"{SchemeFormTestData.Identifier} - {SchemeFormTestData.Name}", Assert.Single(details.Tags("h1")).Text);
        Assert.Equal("Edit Scheme", Assert.Single(edit.Tags("h1")).Text);
        Assert.Empty(details.Tags("form"));
        Assert.Single(edit.Tags("form"));
        Assert.Contains(details.Tags("a"), a => a.Text == "Edit scheme");
        Assert.DoesNotContain(edit.Tags("a"), a => a.Text == "Edit scheme");
        Assert.Empty(details.Tags("button"));
        Assert.Contains(edit.Tags("button"), b => b.Text == "Apply");
        Assert.NotEqual(Fingerprint(details), Fingerprint(edit));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_EditSchemeLinkOnDetails_OpensTheLimitedEditForm(bool requiresAssessment)
    {
        _schemes.SchemeResponse = SchemeFormTestData.Scheme(requiresAssessment, isReadOnly: true);
        var client = _factory.CreateClient();
        var details = await OpenAsync(client, $"/Scheme/Details/{SchemeFormTestData.SchemeId}");

        var href = Assert.Single(details.Tags("a"), a => a.Text == "Edit scheme")["href"];
        Assert.False(string.IsNullOrEmpty(href));
        var edit = await OpenAsync(client, href);

        Assert.Equal("Edit Scheme", Assert.Single(edit.Tags("h1")).Text);
        Assert.Single(edit.Tags("button"), b => b.Text == "Apply");
        Assert.Single(edit.Tags("a"), a => a.Text == "Back");
        edit.AssertLockedText("Identifier", SchemeFormTestData.Identifier);
        Assert.Equal("Biological samples", edit.AssertEditable("CustomsDescription").Text);
    }

    [Fact]
    public async Task PreviousYearScheme_Edit_HasTheEditSchemePageTitle()
    {
        _schemes.SchemeResponse = SchemeFormTestData.Scheme(requiresAssessment: false, isReadOnly: true);

        var html = await _factory.CreateClient().GetStringAsync($"/Scheme/Edit/{SchemeFormTestData.SchemeId}");

        Assert.StartsWith("Edit scheme - ", System.Text.RegularExpressions.Regex.Match(html, "<title>(.*?)</title>", System.Text.RegularExpressions.RegexOptions.Singleline, TimeSpan.FromSeconds(1)).Groups[1].Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_Edit_ShowsApplyAndBackInPlaceOfSaveAndCancel(bool requiresAssessment)
    {
        var page = await PreviousYearPageAsync("Edit", requiresAssessment);

        Assert.Equal("Edit Scheme", Assert.Single(page.Tags("h1")).Text);

        var form = Assert.Single(page.Tags("form"));
        Assert.Equal("post", form["method"], ignoreCase: true);
        Assert.EndsWith($"/Scheme/Apply/{SchemeFormTestData.SchemeId}", form["action"], StringComparison.Ordinal);
        Assert.Single(page.Named("__RequestVerificationToken"));

        var apply = Assert.Single(page.Tags("button"), b => b.Text == "Apply");
        Assert.Equal("submit", apply["type"]);
        Assert.DoesNotContain("govuk-button--secondary", apply["class"], StringComparison.Ordinal);

        var back = Assert.Single(page.Tags("a"), a => a.Text == "Back");
        Assert.Equal("/Scheme/Index", back["href"]);
        Assert.Contains("govuk-button--secondary", back["class"], StringComparison.Ordinal);

        Assert.DoesNotContain(page.Tags("button"), b => b.Text is "Save" or "Cancel");
        Assert.DoesNotContain(page.Tags("a"), a => a.Text is "Save" or "Cancel" or "Edit scheme");
        Assert.DoesNotContain("Read only (closed year)", page.Main, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-summary-list", page.Main, StringComparison.Ordinal);
    }

    // ---- Locked fields still carry their value but cannot be edited ----

    [Fact]
    public async Task PreviousYearScheme_LocksTextFieldsAndKeepsTheirValues()
    {
        var scheme = SchemeFormTestData.Scheme(requiresAssessment: false);
        var page = await PreviousYearPageAsync("Edit", requiresAssessment: false);

        page.AssertLockedText("Identifier", SchemeFormTestData.Identifier);
        page.AssertLockedText("Name", SchemeFormTestData.Name);
        page.AssertLockedText("NumberOfSamples", scheme.NumberOfSamples.ToString(CultureInfo.InvariantCulture));
        page.AssertLockedText("SampleOrigin", scheme.SampleOrigin);
        page.AssertLockedText("Subcontractor", SchemeFormTestData.Subcontractor);
        page.AssertLockedText("Deadline", scheme.Deadline.ToString(CultureInfo.InvariantCulture));
        page.AssertLockedTextArea("SamplePackingInstructions", SchemeFormTestData.PackingInstructions);

        for (var i = 0; i < scheme.Prices!.Count; i++)
        {
            var price = Assert.Single(page.Named($"Prices[{i}].Price"));
            Assert.Equal("text", price["type"]);
            Assert.True(price.Has("readonly"), $"Prices[{i}].Price should be read-only.");
            Assert.False(price.Has("disabled"), $"Prices[{i}].Price must not be disabled, or it would not post its value.");
            Assert.Equal(scheme.Prices[i].Price, decimal.Parse(price["value"]!, CultureInfo.InvariantCulture));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_LocksSelectsAndKeepsTheirValues(bool requiresAssessment)
    {
        var page = await PreviousYearPageAsync("Edit", requiresAssessment);

        page.AssertLockedSelect("ScheduleId", SchemeFormTestData.ScheduleId.ToString(), "Quarterly Schedule");
        page.AssertLockedSelect("ScheduleCodeId", SchemeFormTestData.ScheduleCodeId.ToString(), "SC-01");
        page.AssertLockedSelect("WeekNumber", "3", "3");
        page.AssertLockedSelect("DayOfWeekId", SchemeFormTestData.DayId.ToString(), "Wednesday");
        page.AssertLockedSelect("Postage", SchemeFormTestData.PostageId.ToString(), "Standard Postage");

        // Only the staff of the scheme's own kind are set; the other kind is locked empty.
        foreach (var (field, id, text) in requiresAssessment ? AssessorStaff : ConsultantStaff)
        {
            page.AssertLockedSelect(field, id?.ToString(), text);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_LocksCheckboxesAndKeepsTheirValues(bool requiresAssessment)
    {
        var scheme = SchemeFormTestData.Scheme(requiresAssessment);
        var page = await PreviousYearPageAsync("Edit", requiresAssessment);

        foreach (var (field, value) in SchemeFormTestData.Months(scheme).Concat(SchemeFormTestData.LockedFlags(scheme)))
        {
            page.AssertLockedCheckbox(field, value);
        }

        page.AssertLockedCheckbox("StoreRatings", scheme.StoreRatings);

        // Requires Assessment is a disabled tick, and its value rides in a hidden field that the form's own companion cannot override.
        var tick = Assert.Single(page.Controls, c => c["type"] == "checkbox" && c["name"] == "RequiresAssessment");
        Assert.True(tick.Has("disabled"));
        Assert.Equal(requiresAssessment, tick.Has("checked"));
        Assert.Equal(requiresAssessment, bool.Parse(page.Posting("RequiresAssessment").First()["value"]!));
    }

    [Fact]
    public async Task PreviousYearScheme_KeepsTheFieldsLegacyLeavesEditableEditable()
    {
        var scheme = SchemeFormTestData.Scheme(requiresAssessment: false);
        var page = await PreviousYearPageAsync("Edit", requiresAssessment: false);

        Assert.Equal(scheme.CustomsVolume, page.AssertEditable("CustomsVolume")["value"]);
        Assert.Equal(scheme.CustomsDescription, page.AssertEditable("CustomsDescription").Text);
        Assert.Equal(scheme.DataConsentDeclarationText, page.AssertEditable("DataConsentDeclarationText").Text);
        Assert.Equal(scheme.Instructions, page.AssertEditable("Instructions").Text);
        Assert.Equal(scheme.StandardTabulationText, page.AssertEditable("StandardTabulationText").Text);

        (string Field, bool Ticked)[] checkboxes =
        [
            ("DataConsentDeclarationActive", scheme.DataConsentDeclarationActive),
            ("DateOfReceipt", scheme.DateOfReceipt),
            ("StorageConditions", scheme.StorageConditions),
            ("ConditionOnReceipt", scheme.ConditionOnReceipt),
            ("UseExternalReference", scheme.UseExternalReference),
        ];
        foreach (var (field, ticked) in checkboxes)
        {
            var box = page.AssertEditable(field);
            Assert.Equal("checkbox", box["type"]);
            Assert.Equal(ticked, box.Has("checked"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_KeepsTabulationsViewersAndTestConsultantTabulationEditable(bool requiresAssessment)
    {
        var scheme = SchemeFormTestData.Scheme(requiresAssessment);
        var page = await PreviousYearPageAsync("Edit", requiresAssessment);

        string[] availabilities = ["All", "ViewersAndTestConsultantsOnly"];
        for (var i = 0; i < scheme.Tabulations!.Count; i++)
        {
            var tabulation = scheme.Tabulations[i];
            Assert.Equal(tabulation.Name, page.AssertEditable($"Tabulations[{i}].Name")["value"]);
            Assert.Equal(tabulation.IntendedResultsOnly, page.AssertEditable($"Tabulations[{i}].IntendedResultsOnly").Has("checked"));
            Assert.Equal(tabulation.SingleParticipantTabulation, page.AssertEditable($"Tabulations[{i}].SingleParticipantTabulation").Has("checked"));
            Assert.Equal(tabulation.ShowRatings, page.AssertEditable($"Tabulations[{i}].ShowRatings").Has("checked"));

            var availability = page.AssertEditable($"Tabulations[{i}].Availability");
            Assert.Equal("select", availability.Name);
            Assert.Equal([availabilities[i]], SchemeFormPage.SelectedValues(availability));

            Assert.Equal(tabulation.ResultItemIds.Select(id => id.ToString()), page.Posting($"Tabulations[{i}].ResultItemIds").Select(c => c["value"]!));
            Assert.Equal(tabulation.MethodItemIds.Select(id => id.ToString()), page.Posting($"Tabulations[{i}].MethodItemIds").Select(c => c["value"]!));
        }

        // Every Result and Method Item has a box under every tabulation, none of them locked.
        var itemBoxes = page.Controls
            .Where(c => c["name"] is { } n && n.StartsWith("Tabulations[", StringComparison.Ordinal)
                && (n.EndsWith(".ResultItemIds", StringComparison.Ordinal) || n.EndsWith(".MethodItemIds", StringComparison.Ordinal)))
            .ToList();
        Assert.Equal(scheme.Tests!.Sum(t => t.ResultItems.Count + t.MethodItems.Count) * scheme.Tabulations.Count, itemBoxes.Count);
        Assert.All(itemBoxes, box =>
        {
            Assert.False(box.Has("readonly"));
            Assert.False(box.Has("disabled"));
        });

        var viewers = page.Named("ViewerIds").ToList();
        Assert.Equal(2, viewers.Count);
        Assert.All(viewers, v =>
        {
            Assert.Equal("checkbox", v["type"]);
            Assert.False(v.Has("disabled"));
        });
        Assert.Equal(scheme.ViewerIds!.Select(id => id.ToString()), page.Posting("ViewerIds").Select(v => v["value"]!));
        foreach (var id in (string[])["availableViewers", "schemeViewers", "btnAddSchemeViewer", "btnRemoveSchemeViewer"])
        {
            Assert.False(page.ById(id).Has("disabled"), $"{id} should be usable.");
        }

        // Test Consultants are only picked a tabulation on a scheme that is not assessed; otherwise it is carried unchanged.
        if (requiresAssessment)
        {
            Assert.DoesNotContain(page.Controls, c => c["name"] == "TestConsultantTabulationId" && c["type"] != "hidden");
            Assert.Equal(SchemeFormTestData.TabulationA.ToString(), page.Posting("TestConsultantTabulationId").First()["value"]);
        }
        else
        {
            var selector = page.AssertEditable("TestConsultantTabulationId");
            Assert.Equal("select", selector.Name);
            Assert.Equal([SchemeFormTestData.TabulationA.ToString()], SchemeFormPage.SelectedValues(selector));
        }
    }

    // ---- The Tests tab: re-ordering only, plus the Results Tabulations tab ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_TestsTab_OffersNoAddOrRemoveCommands(bool requiresAssessment)
    {
        var page = await PreviousYearPageAsync("Edit", requiresAssessment);

        Assert.DoesNotContain(page.Commands, c => AddAndRemoveActions.Contains(c.Split(':')[0]));
        Assert.DoesNotContain(page.Controls, c => c["name"]?.StartsWith("pick:", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(page.Tags("th"), th => th.Text == "Actions");
        Assert.DoesNotContain(
            page.ButtonTexts,
            text => text is "Add New Test" or "Remove Test" or "Add Result Item" or "Add Method Item" or "Add Category Item" or "Add Criterion Item");

        // An empty list says so without pointing at an Add button that is not there.
        Assert.Equal(requiresAssessment ? 2 : 1, page.Tags("p").Count(p => p.Text == "No Items Defined"));
        Assert.DoesNotContain(page.Tags("p"), p => p.Text.Contains("Click '", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_TestsTab_OffersOnlyReordering(bool requiresAssessment)
    {
        var page = await PreviousYearPageAsync("Edit", requiresAssessment);

        var testsTab = page.Commands.Where(c => c.Split(':')[0] is not ("add-tabulation" or "remove-tabulation"));
        var expected = requiresAssessment ? ResultAndMethodReordering.Concat(CategoryAndCriterionActions) : ResultAndMethodReordering;

        Assert.Equal(expected, testsTab);
    }

    [Fact]
    public async Task PreviousYearScheme_WithoutAssessment_HasNoCategories()
    {
        var page = await PreviousYearPageAsync("Edit", requiresAssessment: false);

        Assert.DoesNotContain(page.Tags("h4"), h => h.Text is "Categories" or "Criteria");
        Assert.DoesNotContain(page.Controls, c => c["name"]?.Contains(".Categories[", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(page.Commands, c => c.Contains("category", StringComparison.Ordinal) || c.Contains("criterion", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreviousYearScheme_WithAssessment_ShowsEachTestsCategoriesAndCriteria()
    {
        var page = await PreviousYearPageAsync("Edit", requiresAssessment: true);

        Assert.Equal(2, page.Tags("h4").Count(h => h.Text == "Categories"));
        Assert.Equal(3, page.Tags("h4").Count(h => h.Text == "Criteria"));

        var categories = page.Controls
            .Where(c => c["name"] is { } n && n.Contains(".Categories[", StringComparison.Ordinal)
                && n.EndsWith("].Name", StringComparison.Ordinal) && !n.Contains(".Criteria[", StringComparison.Ordinal))
            .Select(c => c["value"]);
        Assert.Equal(["Appearance", "Odour", "Growth"], categories);
        Assert.Equal(["Clear", "Cloudy", "Turbid"], Enumerable.Range(0, 3).Select(i => page.Named($"Tests[0].Categories[0].Criteria[{i}].Name").Single()["value"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousYearScheme_ResultsTabulationsTab_StillAddsAndRemovesTabulations(bool requiresAssessment)
    {
        var page = await PreviousYearPageAsync("Edit", requiresAssessment);

        Assert.Equal(
            ["remove-tabulation:0", "remove-tabulation:1", "add-tabulation"],
            page.Commands.Where(c => c.Contains("tabulation", StringComparison.Ordinal)));
        Assert.Equal("Add New Tabulation", Assert.Single(page.Tags("button"), b => b["value"] == "add-tabulation").Text);
        page.AssertEditable("NewTabulationName");
    }

    // ---- Everything else is unchanged ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Details_WithNonReadOnlyScheme_StillShowsTheViewModePage(bool requiresAssessment)
    {
        var page = await OpenYearPageAsync("Details", requiresAssessment);

        Assert.Equal($"{SchemeFormTestData.Identifier} - {SchemeFormTestData.Name}", Assert.Single(page.Tags("h1")).Text);
        Assert.Equal($"/Scheme/Edit/{SchemeFormTestData.SchemeId}", Assert.Single(page.Tags("a"), a => a.Text == "Edit scheme")["href"]);

        // A view, not a form: nothing to submit and nothing to type into.
        Assert.Empty(page.Tags("form"));
        Assert.Empty(page.Tags("button"));
        Assert.DoesNotContain(page.Tags("a"), a => a.Text is "Apply" or "Back" or "Save" or "Cancel");
        Assert.All(page.Controls, c => Assert.Equal("hidden", c["type"]));

        var summary = page.SummaryRows;
        Assert.Contains(("Identifier", SchemeFormTestData.Identifier), summary);
        Assert.Contains(("Name", SchemeFormTestData.Name), summary);
        Assert.Contains(("Year", "2026/27"), summary);
        Assert.Contains(("Schedule", "Quarterly Schedule"), summary);
        Assert.Contains(("Schedule Code", "SC-01"), summary);
        Assert.Contains(("Distribution months", requiresAssessment ? "As Available" : "Apr, Jul, Oct"), summary);
        Assert.Contains(("Week No", "3"), summary);
        Assert.Contains(("Day of Week", "Wednesday"), summary);
        Assert.Contains(("Number of Samples", "5"), summary);
        Assert.Contains(("Subcontractor", SchemeFormTestData.Subcontractor), summary);
        Assert.Contains(("Postage Type", "Standard Postage"), summary);
        Assert.Contains(("Requires Assessment", requiresAssessment ? "Yes" : "No"), summary);
        Assert.Contains(requiresAssessment ? ("Primary Assessor", "Alice Assessor") : ("Primary Test Consultant", "Ada Internal"), summary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Edit_WithNonReadOnlyScheme_StillShowsTheEditableForm(bool requiresAssessment)
    {
        var page = await OpenYearPageAsync("Edit", requiresAssessment);

        Assert.Equal("Edit Scheme", Assert.Single(page.Tags("h1")).Text);
        Assert.EndsWith($"/Scheme/Edit/{SchemeFormTestData.SchemeId}", Assert.Single(page.Tags("form"))["action"], StringComparison.Ordinal);
        AssertSaveApplyAndCancel(page);

        // Nothing is locked: the fields Previous Year locks are ordinary controls, and the only read-only or disabled ones are the usual few.
        Assert.Equal(SchemeFormTestData.Identifier, page.AssertEditable("Identifier")["value"]);
        Assert.Equal(SchemeFormTestData.Name, page.AssertEditable("Name")["value"]);
        Assert.All(LockedFields, field => page.AssertEditable(field));
        Assert.Equal(["YearLabel", "StartDate", "SampleNoSequence"], page.Controls.Where(c => c.Has("readonly")).Select(c => c["id"]));
        Assert.Equal(["RequiresAssessment"], page.Controls.Where(c => c.Has("disabled")).Select(c => c["id"]));

        // The Tests tab still adds, removes and moves Tests and items.
        string[] commands =
        [
            "add-test", "move-test-down:0", "move-test-up:1", "remove-test:0", "remove-test:1",
            "add-result:0", "remove-result:0:0", "add-method:0", "remove-method:0:0",
        ];
        Assert.All(commands, command => Assert.Contains(command, page.Commands));
        Assert.Contains(page.Controls, c => c["name"] == "pick:add-test");
        Assert.Contains(page.Tags("th"), th => th.Text == "Actions");

        Assert.Equal(requiresAssessment, page.Commands.Contains("add-category:0"));
        Assert.Equal(requiresAssessment, page.Commands.Contains("add-criterion:0:0"));
        Assert.Equal(requiresAssessment, page.Commands.Contains("remove-criterion:0:0:0"));
    }

    [Fact]
    public async Task Create_StillShowsTheEmptyEditableForm()
    {
        var page = await PageAsync("/Scheme/Create", scheme: null);

        Assert.Equal("Create Scheme", Assert.Single(page.Tags("h1")).Text);
        Assert.Equal("/Scheme/Create", Assert.Single(page.Tags("form"))["action"]);
        AssertSaveApplyAndCancel(page);

        Assert.Equal(string.Empty, page.AssertEditable("Identifier")["value"]);
        Assert.Equal(string.Empty, page.AssertEditable("Name")["value"]);
        Assert.Equal(["YearLabel", "StartDate", "SampleNoSequence"], page.Controls.Where(c => c.Has("readonly")).Select(c => c["id"]));
        Assert.DoesNotContain(page.Controls, c => c.Has("disabled"));
        Assert.Contains("add-test", page.Commands);
    }

    [Fact]
    public async Task Create_PrimaryTestConsultantOffersInternalAndExternalConsultants()
    {
        var page = await PageAsync("/Scheme/Create", scheme: null);

        var primary = Assert.Single(page.Named("TestConsultant1"));
        var deputy = Assert.Single(page.Named("TestConsultant2"));

        Assert.Equal(["- Please Select -", "Ada Internal", "Ben Deputy", "Cleo External"], SchemeFormPage.OptionTexts(primary));
        Assert.Equal(["None", "Ada Internal", "Ben Deputy", "Cleo External"], SchemeFormPage.OptionTexts(deputy));
    }

    [Theory]
    [InlineData("Details")]
    [InlineData("Edit")]
    public async Task UnknownScheme_ReturnsNotFound(string route)
    {
        _schemes.SchemeResponse = null;

        var response = await _factory.CreateClient().GetAsync($"/Scheme/{route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Apply through the pipeline: antiforgery, model binding and the redisplayed form ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_WithTheRenderedForm_SavesTheSchemeUnchangedAndStaysOnTheEditPage(bool requiresAssessment)
    {
        var scheme = SchemeFormTestData.Scheme(requiresAssessment);
        _schemes.SchemeResponse = scheme;
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await OpenAsync(client, $"/Scheme/Edit/{SchemeFormTestData.SchemeId}");

        var response = await ApplyAsync(client, page);

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await RedisplayedErrorsAsync(response));
        Assert.Equal($"/Scheme/Edit/{SchemeFormTestData.SchemeId}", response.Headers.Location?.OriginalString);

        var saved = _schemes.LastUpdateRequest;
        Assert.NotNull(saved);
        Assert.Equal(
            (scheme.YearId, scheme.Identifier, scheme.Name, scheme.ScheduleId, scheme.ScheduleCodeId, scheme.WeekNumber, scheme.DayOfWeekId, scheme.NumberOfSamples),
            (saved.YearId, saved.Identifier, saved.Name, saved.ScheduleId, saved.ScheduleCodeId, saved.WeekNumber, saved.DayOfWeekId, saved.NumberOfSamples));
        Assert.Equal(
            (scheme.SampleOrigin, scheme.Deadline, scheme.Subcontractor, scheme.Postage, scheme.SamplePackingInstructions, scheme.RequiresAssessment, scheme.StoreRatings),
            (saved.SampleOrigin, saved.Deadline, saved.Subcontractor, saved.Postage, saved.SamplePackingInstructions, saved.RequiresAssessment, saved.StoreRatings));
        foreach (var (field, value) in SchemeFormTestData.Months(scheme).Concat(SchemeFormTestData.LockedFlags(scheme)))
        {
            Assert.Equal(value, (bool)typeof(SchemeRequest).GetProperty(field)!.GetValue(saved)!);
        }

        Assert.Equal(
            (scheme.TestConsultant1, scheme.TestConsultant2, scheme.TestConsultant3, scheme.Assessor1, scheme.Assessor2, scheme.Assessor3, scheme.Assessor4, scheme.TestConsultantTabulationId),
            (saved.TestConsultant1, saved.TestConsultant2, saved.TestConsultant3, saved.Assessor1, saved.Assessor2, saved.Assessor3, saved.Assessor4, saved.TestConsultantTabulationId));
        Assert.Equal(
            (scheme.CustomsVolume, scheme.CustomsDescription, scheme.DataConsentDeclarationActive, scheme.DataConsentDeclarationText, scheme.Instructions, scheme.StandardTabulationText),
            (saved.CustomsVolume, saved.CustomsDescription, saved.DataConsentDeclarationActive, saved.DataConsentDeclarationText, saved.Instructions, saved.StandardTabulationText));
        Assert.Equal(
            (scheme.DateOfReceipt, scheme.StorageConditions, scheme.ConditionOnReceipt, scheme.UseExternalReference),
            (saved.DateOfReceipt, saved.StorageConditions, saved.ConditionOnReceipt, saved.UseExternalReference));

        Assert.Equal(scheme.Prices!.Select(p => (p.SchemeCurrencyId, p.CurrencyId, p.Price)), saved.Prices!.Select(p => (p.SchemeCurrencyId, p.CurrencyId, p.Price)));
        Assert.Equal(scheme.ViewerIds, saved.ViewerIds);
        Assert.Equal(TestIds(scheme.Tests!, withCategories: requiresAssessment), TestIds(saved.Tests!));
        Assert.Equivalent(scheme.Tabulations, saved.Tabulations, strict: true);
    }

    // ASP.NET Core rejects a form of more than 1,024 values by default; a scheme with many Tests, Result Items and Categories posts far more.
    private const int LargeSchemeRepeats = 40;

    private static SchemeResponse LargeScheme(bool isReadOnly)
    {
        var scheme = SchemeFormTestData.Scheme(requiresAssessment: true, isReadOnly);
        return scheme with { Tests = [.. Enumerable.Repeat(scheme.Tests!, LargeSchemeRepeats).SelectMany(tests => tests)] };
    }

    private async Task<(HttpClient Client, SchemeFormPage Page, int Expected)> OpenLargeSchemeAsync(bool isReadOnly)
    {
        var scheme = LargeScheme(isReadOnly);
        _schemes.SchemeResponse = scheme;
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await OpenAsync(client, $"/Scheme/Edit/{SchemeFormTestData.SchemeId}");

        Assert.True(page.Submission().Count > 1024, "The test form does not exceed the default form value limit.");
        return (client, page, scheme.Tests!.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditAndApply_FormWithMoreThanTheDefaultFormValueLimit_IsSaved(bool isReadOnly)
    {
        var (client, page, expectedTests) = await OpenLargeSchemeAsync(isReadOnly);

        var response = await ApplyAsync(client, page);

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await RedisplayedErrorsAsync(response));
        Assert.Equal(expectedTests, _schemes.LastUpdateRequest!.Tests!.Count);
    }

    [Fact]
    public async Task Create_FormWithMoreThanTheDefaultFormValueLimit_IsSaved()
    {
        var (client, page, expectedTests) = await OpenLargeSchemeAsync(isReadOnly: false);
        _schemes.SaveResult = new SchemeSaveResult(true, SchemeFormTestData.Scheme(requiresAssessment: true, isReadOnly: false), new Dictionary<string, string[]>());

        var response = await client.PostAsync("/Scheme/Create", new FormUrlEncodedContent(page.Submission()));

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await RedisplayedErrorsAsync(response));
        Assert.Equal(expectedTests, _schemes.LastCreateRequest!.Tests!.Count);
    }

    [Fact]
    public async Task Apply_WithAnInvalidEditableField_RedisplaysThePreviousYearFormWithTheError()
    {
        _schemes.SchemeResponse = SchemeFormTestData.Scheme(requiresAssessment: false);
        var client = _factory.CreateClient();
        var page = await OpenAsync(client, $"/Scheme/Edit/{SchemeFormTestData.SchemeId}");
        var tooLong = new string('x', 21);

        var response = await ApplyAsync(client, page, fields => SetField(fields, "CustomsVolume", tooLong));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var redisplayed = SchemeFormPage.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Customs volume must not exceed 20 characters", Assert.Single(redisplayed.Tags("a"), a => a["href"] == "#CustomsVolume").Text);
        Assert.Equal(tooLong, redisplayed.AssertEditable("CustomsVolume")["value"]);

        Assert.EndsWith($"/Scheme/Apply/{SchemeFormTestData.SchemeId}", Assert.Single(redisplayed.Tags("form"))["action"], StringComparison.Ordinal);
        Assert.Single(redisplayed.Tags("button"), b => b.Text == "Apply");
        redisplayed.AssertLockedText("Identifier", SchemeFormTestData.Identifier);
        Assert.Null(_schemes.LastUpdateRequest);
    }

    [Fact]
    public async Task Apply_WithAReorderCommand_RedisplaysTheFormWithTheItemsSwapped()
    {
        _schemes.SchemeResponse = SchemeFormTestData.Scheme(requiresAssessment: false);
        var client = _factory.CreateClient();
        var page = await OpenAsync(client, $"/Scheme/Edit/{SchemeFormTestData.SchemeId}");
        Assert.Contains("move-result-down:0:0", page.Commands);

        var response = await ApplyAsync(client, page, command: "move-result-down:0:0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var redisplayed = SchemeFormPage.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ["Seroconversion", "Titre", "Antibody status"],
            Enumerable.Range(0, 3).Select(i => redisplayed.Named($"Tests[0].ResultItems[{i}].Name").Single()["value"]));
        Assert.Single(redisplayed.Tags("button"), b => b.Text == "Apply");
        Assert.Null(_schemes.LastUpdateRequest);
    }
}
