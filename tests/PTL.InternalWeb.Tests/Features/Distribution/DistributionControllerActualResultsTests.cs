using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Actual Results screen - legacy Internal User/ActualResults.aspx (reached from Results Receipt "View/Edit").
public class DistributionControllerActualResultsTests
{
    private const int Year = 2026;
    private const int Month = 5;

    private static readonly Guid SchemeId = Guid.Parse("a0000000-0000-0000-0000-0000000000a1");
    private static readonly Guid ParticipantId = Guid.Parse("d0000000-0000-0000-0000-0000000000d1");
    private static readonly Guid TestId = Guid.Parse("e0000000-0000-0000-0000-0000000000e1");
    private static readonly Guid DropdownItem = Guid.Parse("c0000000-0000-0000-0000-0000000000c1");
    private static readonly Guid TextItem = Guid.Parse("c0000000-0000-0000-0000-0000000000c2");
    private static readonly Guid Sample1 = Guid.Parse("50000000-0000-0000-0000-000000000051");
    private static readonly Guid Sample2 = Guid.Parse("50000000-0000-0000-0000-000000000052");
    private static readonly Guid Value11 = Guid.Parse("b0000000-0000-0000-0000-000000000011");
    private static readonly Guid Value12 = Guid.Parse("b0000000-0000-0000-0000-000000000012");
    private static readonly Guid Value21 = Guid.Parse("b0000000-0000-0000-0000-000000000021");
    private static readonly Guid Value22 = Guid.Parse("b0000000-0000-0000-0000-000000000022");
    private static readonly Guid MethodText = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid MethodDropdown = Guid.Parse("a1000000-0000-0000-0000-000000000002");

    private static DistributionController CreateController(FakeDistributionApiClient apiClient) =>
        new(apiClient, new FakeSearchApiClient())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
        };

    private static ActualResultsResponse Page() =>
        new(ParticipantId, SchemeId, "D1 BT: Brucella", "Results Received for D1 BT: Brucella", new DateTime(2026, 6, 30), false,
        [
            new ActualResultsTestResponse(TestId, "Culture test",
                [
                    new ActualResultsResultItemResponse(DropdownItem, "Growth", "Positive|Negative", "Positive or Negative"),
                    new ActualResultsResultItemResponse(TextItem, "Count", "^\\d+$", "Whole number"),
                ],
                [
                    new ActualResultsMethodItemResponse(MethodText, "Incubation", "^.+$", "Text", "37C"),
                    new ActualResultsMethodItemResponse(MethodDropdown, "Medium", "Agar|Broth", "Agar or Broth", "Broth"),
                ],
                [
                    new ActualResultsSampleRowResponse(Sample1, "S001",
                    [
                        new ActualResultsSampleValueResponse(Value11, DropdownItem, "Positive"),
                        new ActualResultsSampleValueResponse(Value12, TextItem, "12"),
                    ]),
                    new ActualResultsSampleRowResponse(Sample2, "S002",
                    [
                        new ActualResultsSampleValueResponse(Value21, DropdownItem, ""),
                        new ActualResultsSampleValueResponse(Value22, TextItem, ""),
                    ]),
                ]),
        ]);

    private static ActualResultsViewModel Posted(
        string value12 = "99", string value22 = "7", string methodText = "40C", string? auditWhy = null, bool isAuditStep = false, string? pendingCommand = null) =>
        new()
        {
            IsAuditStep = isAuditStep,
            PendingCommand = pendingCommand,
            AuditWhy = auditWhy,
            Tests =
            [
                new ActualResultsTestViewModel
                {
                    TestId = TestId,
                    Samples =
                    [
                        new ActualResultsSampleRowViewModel
                        {
                            SampleId = Sample1,
                            Values =
                            [
                                new ActualResultsSampleValueViewModel { SampleResultItemId = Value11, TestResultItemId = DropdownItem, Result = "Negative" },
                                new ActualResultsSampleValueViewModel { SampleResultItemId = Value12, TestResultItemId = TextItem, Result = value12 },
                            ],
                        },
                        new ActualResultsSampleRowViewModel
                        {
                            SampleId = Sample2,
                            Values =
                            [
                                new ActualResultsSampleValueViewModel { SampleResultItemId = Value21, TestResultItemId = DropdownItem, Result = null },
                                new ActualResultsSampleValueViewModel { SampleResultItemId = Value22, TestResultItemId = TextItem, Result = value22 },
                            ],
                        },
                    ],
                    MethodItems =
                    [
                        new ActualResultsMethodItemViewModel { TestMethodItemId = MethodText, Result = methodText },
                        new ActualResultsMethodItemViewModel { TestMethodItemId = MethodDropdown, Result = null },
                    ],
                },
            ],
        };

    private static Task<IActionResult> PostAsync(DistributionController controller, ActualResultsViewModel model, string command) =>
        controller.ActualResults(Year, Month, SchemeId, ParticipantId, model, command, CancellationToken.None);

    private static void AssertRedirect(IActionResult result, string action, bool carriesParticipant)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(action, redirect.ActionName);
        Assert.Equal(Year, redirect.RouteValues!["year"]);
        Assert.Equal(Month, redirect.RouteValues["monthId"]);
        Assert.Equal(carriesParticipant, redirect.RouteValues.ContainsKey("participantId"));
        if (carriesParticipant)
        {
            Assert.Equal(SchemeId, redirect.RouteValues["schemeId"]);
            Assert.Equal(ParticipantId, redirect.RouteValues["participantId"]);
        }
    }

    // ---------- GET ----------

    [Fact]
    public async Task Get_ApiReturnsNull_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient { ActualResults = null });

        Assert.IsType<NotFoundResult>(await controller.ActualResults(Year, Month, SchemeId, ParticipantId, CancellationToken.None));
    }

    [Fact]
    public async Task Get_MapsThePageOntoTheViewModel()
    {
        var controller = CreateController(new FakeDistributionApiClient { ActualResults = Page() });

        var result = await controller.ActualResults(Year, Month, SchemeId, ParticipantId, CancellationToken.None);

        var model = Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((Year, Month, SchemeId, ParticipantId), (model.YearId, model.MonthId, model.MonthlyDistributionSchemeId, model.DistributionParticipantId));
        Assert.Equal("D1 BT: Brucella", model.Heading);
        Assert.False(model.IsAuditStep);
        Assert.False(model.SaveFailed);

        var test = Assert.Single(model.Tests);
        Assert.Equal((TestId, "Culture test"), (test.TestId, test.TestName));
        Assert.Equal(["Growth", "Count"], test.ResultItems.Select(r => r.Name));
        Assert.Equal(["Positive|Negative", "^\\d+$"], test.ResultItems.Select(r => r.FormatString));
        Assert.Equal(["Positive or Negative", "Whole number"], test.ResultItems.Select(r => r.ReturnValueType));
        Assert.Equal([MethodText, MethodDropdown], test.MethodItems.Select(m => m.TestMethodItemId));
        Assert.Equal(["37C", "Broth"], test.MethodItems.Select(m => m.Result));
        Assert.Equal(["Incubation", "Medium"], test.MethodItems.Select(m => m.Name));

        Assert.Equal([Sample1, Sample2], test.Samples.Select(s => s.SampleId));
        Assert.Equal(["S001", "S002"], test.Samples.Select(s => s.SampleNumber));
        Assert.Equal([Value11, Value12], test.Samples[0].Values.Select(v => v.SampleResultItemId));
        Assert.Equal([DropdownItem, TextItem], test.Samples[0].Values.Select(v => v.TestResultItemId));
        Assert.Equal(["Positive", "12"], test.Samples[0].Values.Select(v => v.Result));
    }

    // ---------- POST: cancel ----------

    [Fact]
    public async Task Get_PastDeadlinePage_MapsReadOnlyOntoTheViewModel()
    {
        var controller = CreateController(new FakeDistributionApiClient { ActualResults = Page() with { IsReadOnly = true } });

        var result = await controller.ActualResults(Year, Month, SchemeId, ParticipantId, CancellationToken.None);

        Assert.True(Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model).IsReadOnly);
    }

    [Fact]
    public async Task Get_OpenPage_IsNotReadOnly()
    {
        var controller = CreateController(new FakeDistributionApiClient { ActualResults = Page() });

        var result = await controller.ActualResults(Year, Month, SchemeId, ParticipantId, CancellationToken.None);

        Assert.False(Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model).IsReadOnly);
    }

    // ---------- POST: cancel ----------

    [Fact]
    public async Task Post_Cancel_ReturnsToResultsReceiptWithoutCallingTheApi()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = Page() };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(), "cancel");

        AssertRedirect(result, "ResultsReceipt", carriesParticipant: false);
        Assert.Empty(apiClient.SavedActualResultsRequests);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData("apply", "ActualResults", true)]
    [InlineData("save", "ResultsReceipt", false)]
    [InlineData(null, "ResultsReceipt", false)]
    public async Task Post_CancelAudit_ReturnsWhereTheUserWasHeadingWithoutSaving(string? pendingCommand, string expectedAction, bool carriesParticipant)
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = Page() };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(isAuditStep: true, pendingCommand: pendingCommand), "cancelAudit");

        AssertRedirect(result, expectedAction, carriesParticipant);
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    // ---------- POST: save / apply success ----------

    [Fact]
    public async Task Post_SaveSuccess_ShowsSuccessAndReturnsToResultsReceipt()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Page(),
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>()),
        };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(), "save");

        AssertRedirect(result, "ResultsReceipt", carriesParticipant: false);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Results updated successfully.", notification.Message);
    }

    [Fact]
    public async Task Post_ApplySuccess_StaysOnActualResults()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Page(),
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>()),
        };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(), "apply");

        AssertRedirect(result, "ActualResults", carriesParticipant: true);
        Assert.Equal("Results updated successfully.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Post_AuditStepSaveAfterApply_LandsOnActualResultsAndPassesTheReason()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Page(),
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>()),
        };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(auditWhy: "Lab corrected", isAuditStep: true, pendingCommand: "apply"), "save");

        AssertRedirect(result, "ActualResults", carriesParticipant: true);
        Assert.Equal("Lab corrected", Assert.Single(apiClient.SavedActualResultsRequests).AuditWhy);
    }

    [Fact]
    public async Task Post_AuditStepSaveAfterSave_ReturnsToResultsReceipt()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Page(),
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>()),
        };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(auditWhy: "Lab corrected", isAuditStep: true, pendingCommand: "save"), "save");

        AssertRedirect(result, "ResultsReceipt", carriesParticipant: false);
    }

    [Fact]
    public async Task Post_FlattensSampleValuesAndMethodItemsIntoTheRequest_TreatingNullAsEmpty()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Page(),
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>()),
        };
        var controller = CreateController(apiClient);

        await PostAsync(controller, Posted(auditWhy: "why"), "save");

        var request = Assert.Single(apiClient.SavedActualResultsRequests);
        Assert.Equal(
            [
                new ActualResultsSampleValueRequest(Value11, "Negative"),
                new ActualResultsSampleValueRequest(Value12, "99"),
                new ActualResultsSampleValueRequest(Value21, ""),
                new ActualResultsSampleValueRequest(Value22, "7"),
            ],
            request.SampleValues);
        Assert.Equal(
            [new ActualResultsMethodItemRequest(MethodText, "40C"), new ActualResultsMethodItemRequest(MethodDropdown, "")],
            request.MethodItems);
        Assert.Equal("why", request.AuditWhy);
    }

    // ---------- POST: failures ----------

    [Fact]
    public async Task Post_ApiReturnsNull_ShowsErrorAndRedisplaysTheViewWithThePostedValues()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = Page(), ActualResultsSaveResult = null };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(), "save");

        var model = Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Results could not be saved. Please try again later.", notification.Message);
        Assert.Equal("99", model.Tests[0].Samples[0].Values[1].Result);
        Assert.Equal("D1 BT: Brucella", model.Heading);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("apply")]
    public async Task Post_AuditReasonRequired_ShowsTheAuditStepRememberingTheButtonAndTheEdits(string command)
    {
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Page(),
            ActualResultsSaveResult = new ActualResultsSaveResult(
                false, new Dictionary<Guid, string>(), AuditReasonRequired: true, AuditObjectName: "Results Received for D1 BT: Brucella"),
        };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(), command);

        var model = Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.IsAuditStep);
        Assert.Equal("Results Received for D1 BT: Brucella", model.AuditObjectName);
        Assert.Equal(command, model.PendingCommand);
        Assert.False(model.SaveFailed);
        Assert.Equal("99", model.Tests[0].Samples[0].Values[1].Result);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Post_DeadlinePassed_RedisplaysWithSaveFailed()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Page(),
            ActualResultsSaveResult = new ActualResultsSaveResult(false, new Dictionary<Guid, string>(), DeadlinePassed: true),
        };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(), "save");

        var model = Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.SaveFailed);
        Assert.False(model.IsAuditStep);
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Post_FieldErrors_AreAddedToModelStateUnderTheIndexedInputNames()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Page(),
            ActualResultsSaveResult = new ActualResultsSaveResult(
                false,
                new Dictionary<Guid, string>
                {
                    [Value22] = "Input must be of format: Whole number",
                    [MethodText] = "Input must be of format: Text",
                }),
        };
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, Posted(value22: "abc", methodText: ""), "save");

        var model = Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.SaveFailed);
        Assert.Equal(2, controller.ModelState.ErrorCount);
        Assert.Equal(
            "Input must be of format: Whole number",
            Assert.Single(controller.ModelState["Tests[0].Samples[1].Values[1].Result"]!.Errors).ErrorMessage);
        Assert.Equal(
            "Input must be of format: Text",
            Assert.Single(controller.ModelState["Tests[0].MethodItems[0].Result"]!.Errors).ErrorMessage);
        Assert.Equal("abc", model.Tests[0].Samples[1].Values[1].Result);
        Assert.Empty(controller.TempData);
    }

    [Fact]
    public async Task Post_AuditStepWithBlankReason_RedisplaysWithoutCallingTheApi()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = Page() };
        var controller = CreateController(apiClient);
        var posted = Posted(auditWhy: "  ", isAuditStep: true, pendingCommand: "apply");
        foreach (var error in posted.Validate(new ValidationContext(posted)))
        {
            controller.ModelState.AddModelError(error.MemberNames.Single(), error.ErrorMessage!);
        }

        var result = await PostAsync(controller, posted, "save");

        var model = Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("The reason for these changes must be specified", Assert.Single(controller.ModelState["AuditWhy"]!.Errors).ErrorMessage);
        Assert.Empty(apiClient.SavedActualResultsRequests);
        Assert.True(model.IsAuditStep);
        Assert.Equal("apply", model.PendingCommand);
        Assert.Equal("  ", model.AuditWhy);
        Assert.Equal("Negative", model.Tests[0].Samples[0].Values[0].Result);
        Assert.Equal("99", model.Tests[0].Samples[0].Values[1].Result);
        Assert.Equal("40C", model.Tests[0].MethodItems[0].Result);
        Assert.Equal("D1 BT: Brucella", model.Heading);
    }

    [Fact]
    public async Task Post_ModelStateInvalidAndThePageHasGone_RedisplaysThePostedModel()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = null };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "The reason for these changes must be specified");
        var posted = Posted(isAuditStep: true);

        var result = await PostAsync(controller, posted, "save");

        var model = Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Same(posted, model);
        Assert.Equal((Year, Month, SchemeId, ParticipantId), (model.YearId, model.MonthId, model.MonthlyDistributionSchemeId, model.DistributionParticipantId));
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Post_PostedValuesForUnknownItemsAreIgnoredOnRedisplay()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = Page(), ActualResultsSaveResult = null };
        var controller = CreateController(apiClient);
        var posted = Posted();
        posted.Tests[0].Samples[0].Values.Add(new ActualResultsSampleValueViewModel { SampleResultItemId = Guid.NewGuid(), Result = "stray" });
        posted.Tests[0].MethodItems.Add(new ActualResultsMethodItemViewModel { TestMethodItemId = Guid.NewGuid(), Result = "stray" });

        var result = await PostAsync(controller, posted, "save");

        var model = Assert.IsType<ActualResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(2, model.Tests[0].Samples[0].Values.Count);
        Assert.Equal(2, model.Tests[0].MethodItems.Count);
    }
}

public class ActualResultsViewModelTests
{
    [Theory]
    [InlineData(true, "", false)]
    [InlineData(true, "   ", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "Lab corrected", true)]
    [InlineData(false, "", true)]
    [InlineData(false, null, true)]
    public void Validate_RequiresAReasonOnlyOnTheAuditStep(bool isAuditStep, string? auditWhy, bool expectedValid)
    {
        var model = new ActualResultsViewModel { IsAuditStep = isAuditStep, AuditWhy = auditWhy };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Equal(expectedValid, results.Count == 0);
        if (!expectedValid)
        {
            var error = Assert.Single(results);
            Assert.Equal("The reason for these changes must be specified", error.ErrorMessage);
            Assert.Equal([nameof(ActualResultsViewModel.AuditWhy)], error.MemberNames);
        }
    }

    [Fact]
    public void AuditWhy_IsLimitedTo2000Characters()
    {
        var model = new ActualResultsViewModel { AuditWhy = new string('x', 2001) };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Equal("Input can not exceed 2000 characters", Assert.Single(results).ErrorMessage);
    }

    [Theory]
    [InlineData("Positive|Negative", true, new[] { "Positive", "Negative" })]
    [InlineData("Agar||Broth|", true, new[] { "Agar", "Broth" })]
    [InlineData("", true, new string[0])]
    [InlineData(null, true, new string[0])]
    [InlineData("^\\d+$", false, new string[0])]
    public void ResultItem_DropdownHelpers_FollowTheLegacyFormatStringRule(string? formatString, bool isDropdown, string[] options)
    {
        var item = new ActualResultsResultItemViewModel { FormatString = formatString, ReturnValueType = "Whole number" };

        Assert.Equal(isDropdown, item.IsDropdown);
        Assert.Equal(options, item.DropdownOptions);
        Assert.Equal("Input must be of format: Whole number", item.InputFormatMessage);
    }

    [Theory]
    [InlineData("Agar|Broth", true, new[] { "Agar", "Broth" })]
    [InlineData(null, true, new string[0])]
    [InlineData("^.+$", false, new string[0])]
    public void MethodItem_DropdownHelpers_FollowTheLegacyFormatStringRule(string? formatString, bool isDropdown, string[] options)
    {
        var item = new ActualResultsMethodItemViewModel { FormatString = formatString, ReturnValueType = "Text" };

        Assert.Equal(isDropdown, item.IsDropdown);
        Assert.Equal(options, item.DropdownOptions);
        Assert.Equal("Input must be of format: Text", item.InputFormatMessage);
    }

    [Fact]
    public void ValueIndexFor_ReturnsThePositionOfTheResultItemWithinTheSampleRow_OrMinusOne()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var test = new ActualResultsTestViewModel
        {
            Samples =
            [
                new ActualResultsSampleRowViewModel
                {
                    Values = [new ActualResultsSampleValueViewModel { TestResultItemId = first }, new ActualResultsSampleValueViewModel { TestResultItemId = second }],
                },
                new ActualResultsSampleRowViewModel { Values = [new ActualResultsSampleValueViewModel { TestResultItemId = second }] },
            ],
        };

        Assert.Equal(1, test.ValueIndexFor(0, second));
        Assert.Equal(0, test.ValueIndexFor(1, second));
        Assert.Equal(-1, test.ValueIndexFor(1, first));
    }
}
