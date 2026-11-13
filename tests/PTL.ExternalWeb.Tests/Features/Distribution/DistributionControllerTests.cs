using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Distribution;
using PTL.ExternalWeb.Tests.TestSupport;
using PTL.ExternalWeb.ViewSupport;

namespace PTL.ExternalWeb.Tests.Features.Distribution;

// Previous/Results behaviour is covered end-to-end by Integration/DistributionPreviousAndResultsRouteSmokeTests.cs
// (they need a real HTTP pipeline for User.GetParticipantId() and the rendered Razor views).
public class DistributionControllerTests
{
    private static readonly string[] UnsortedReferences = ["B", "A", "C"];

    [Fact]
    public async Task Current_ReturnsMappedCurrentDistributions()
    {
        var participantId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions =
            [
                new CurrentDistributionResponse(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "D26-01A/BA",
                    "PT001",
                    "Bacteriology",
                    new DateTime(2026, 4, 3),
                    new DateTime(2026, 4, 30),
                    HasResults: false,
                    Submitted: false)
            ]
        };

        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, participantId);

        var result = await controller.Current();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CurrentDistributionsViewModel>(view.Model);
        var row = Assert.Single(model.Distributions);
        Assert.Equal("D26-01A/BA", row.DistributionReference);
        Assert.Equal("No", row.Status);
    }

    [Theory]
    [InlineData(false, false, "No")]
    [InlineData(true, false, "In progress")]
    [InlineData(true, true, "Submitted")]
    [InlineData(false, true, "Submitted")]
    public void CurrentRow_Status_FollowsLegacyGetStatus(bool hasResults, bool submitted, string expected)
    {
        var row = new CurrentDistributionRowViewModel
        {
            MonthlyDistributionSchemeId = Guid.NewGuid(),
            DistributionParticipantId = Guid.NewGuid(),
            DistributionReference = "D26-01A/BA",
            SchemeCode = "PT001",
            SchemeName = "Bacteriology",
            DistributionDate = new DateTime(2026, 4, 3),
            DeadlineDate = new DateTime(2026, 4, 30),
            HasResults = hasResults,
            Submitted = submitted,
        };

        Assert.Equal(expected, row.Status);
    }

    [Theory]
    [InlineData(false, false, "govuk-tag govuk-tag--grey")]
    [InlineData(true, false, "govuk-tag govuk-tag--light-blue")]
    [InlineData(true, true, "govuk-tag govuk-tag--green")]
    public void CurrentRow_StatusTagClass_MatchesStatus(bool hasResults, bool submitted, string expected)
    {
        var row = new CurrentDistributionRowViewModel
        {
            MonthlyDistributionSchemeId = Guid.NewGuid(),
            DistributionParticipantId = Guid.NewGuid(),
            DistributionReference = "D26-01A/BA",
            SchemeCode = "PT001",
            SchemeName = "Bacteriology",
            DistributionDate = new DateTime(2026, 4, 3),
            DeadlineDate = new DateTime(2026, 4, 30),
            HasResults = hasResults,
            Submitted = submitted,
        };

        Assert.Equal(expected, row.StatusTagClass);
    }

    [Theory]
    [InlineData(null, null, "B,A,C", "distributionDate", "desc")]
    [InlineData("reference", "asc", "A,B,C", "reference", "asc")]
    [InlineData("reference", "desc", "C,B,A", "reference", "desc")]
    [InlineData("nonsense", "sideways", "B,A,C", "distributionDate", "desc")]
    public async Task Current_SortsByTheRequestedColumn_AndUsesDistributionDateDescendingByDefault(
        string? sortBy, string? sortDir, string expectedOrder, string expectedSortBy, string expectedDir)
    {
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions = [.. UnsortedReferences.Select(reference => new CurrentDistributionResponse(
                Guid.NewGuid(), Guid.NewGuid(), reference, "PT001", "Bacteriology",
                new DateTime(2026, 4, 3), new DateTime(2026, 4, 30), HasResults: false, Submitted: false))]
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Current(sortBy, sortDir);

        var model = Assert.IsType<CurrentDistributionsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(expectedOrder, string.Join(",", model.Distributions.Select(d => d.DistributionReference)));
        Assert.Equal(expectedSortBy, model.SortBy);
        Assert.Equal(expectedDir, model.SortDirection);
    }

    [Fact]
    public async Task Current_SortByStatus_OrdersByWhetherResultsExist()
    {
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions =
            [
                new CurrentDistributionResponse(Guid.NewGuid(), Guid.NewGuid(), "WITH", "PT001", "S", new DateTime(2026, 4, 3), new DateTime(2026, 4, 30), HasResults: true, Submitted: false),
                new CurrentDistributionResponse(Guid.NewGuid(), Guid.NewGuid(), "NONE", "PT001", "S", new DateTime(2026, 4, 3), new DateTime(2026, 4, 30), HasResults: false, Submitted: false),
            ]
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Current("status", "asc");

        var model = Assert.IsType<CurrentDistributionsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(["NONE", "WITH"], model.Distributions.Select(d => d.DistributionReference));
    }

    [Fact]
    public async Task Current_NoSignedInParticipant_ReturnsEmptyPagedModel()
    {
        var controller = new DistributionController(new FakeDistributionApiClient())
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        var result = await controller.Current(page: 0, pageSize: 999);

        var model = Assert.IsType<CurrentDistributionsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(1, model.Page);
        Assert.Equal(ReferenceSchemeListQuery.DefaultPageSize, model.PageSize);
        Assert.Empty(model.Distributions);
        Assert.Equal("distributionDate", model.SortBy);
        Assert.Equal("desc", model.SortDirection);
    }

    [Fact]
    public async Task Previous_NoSignedInParticipant_ReturnsEmptyPagedModel()
    {
        var controller = new DistributionController(new FakeDistributionApiClient())
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        var result = await controller.Previous(new ReferenceSchemeListQuery(), CancellationToken.None);

        var model = Assert.IsType<DistributionPreviousListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Distributions);
        Assert.Equal(ReferenceSchemeListQuery.DefaultPageSize, model.PageSize);
        Assert.Equal("distributionDate", model.SortBy);
        Assert.Equal("desc", model.SortDirection);
    }

    [Fact]
    public async Task Previous_UsesLegacyDefaults_FiltersAndBuildsYearOptions()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
            [
                new ParticipantPastDistributionResponse(Guid.NewGuid(), "REF-1", "PTA", "Alpha Beta", new DateTime(2026, 3, 10), 2025, 3),
                new ParticipantPastDistributionResponse(Guid.NewGuid(), "REF-2", "PTB", "Alpha Gamma", new DateTime(2026, 3, 20), 2026, 3),
                new ParticipantPastDistributionResponse(Guid.NewGuid(), "REF-3", "PTC", "Other", new DateTime(2025, 2, 1), 2024, 2),
            ])
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var query = new ReferenceSchemeListQuery
        {
            SortBy = "invalid",
            SortDir = "invalid",
            Year = 2026,
            Month = 3,
            SchemeName = "Alpha Beta"
        };

        var result = await controller.Previous(query, CancellationToken.None);

        var model = Assert.IsType<DistributionPreviousListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("distributionDate", model.SortBy);
        Assert.Equal("desc", model.SortDirection);
        Assert.Single(model.Distributions);
        Assert.Equal("REF-1", model.Distributions[0].DistributionReference);
        Assert.Equal([2024, 2025, 2026], model.Years);
    }

    [Fact]
    public async Task Results_WhenParticipantRecordNotFound_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = null };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Results(Guid.NewGuid(), null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Results_StoresValidatedListReturnUrl()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = OwnResults(schemeId, Guid.NewGuid()) };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Results(schemeId, "/Distribution/Previous?page=2", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("/Distribution/Previous?page=2", controller.ViewData["ListReturnUrl"]);
    }

    [Fact]
    public async Task Results_RejectsUnsafeListReturnUrl()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = OwnResults(schemeId, Guid.NewGuid()) };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        await controller.Results(schemeId, "//evil.example/Distribution/Previous", CancellationToken.None);

        Assert.Null(controller.ViewData["ListReturnUrl"]);
    }

    [Theory]
    [InlineData("reference", "asc", "REF-1,REF-2")]
    [InlineData("reference", "desc", "REF-2,REF-1")]
    [InlineData("schemeCode", "asc", "REF-2,REF-1")]
    [InlineData("schemeName", "asc", "REF-1,REF-2")]
    [InlineData("distributionDate", "asc", "REF-2,REF-1")]
    public async Task Previous_SortsBySupportedColumns(string sortBy, string sortDir, string expectedOrder)
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
            [
                new ParticipantPastDistributionResponse(Guid.NewGuid(), "REF-1", "PTB", "Alpha", new DateTime(2026, 2, 1), 2026, 2),
                new ParticipantPastDistributionResponse(Guid.NewGuid(), "REF-2", "PTA", "Beta", new DateTime(2026, 1, 1), 2026, 1),
            ])
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Previous(new ReferenceSchemeListQuery { SortBy = sortBy, SortDir = sortDir }, CancellationToken.None);

        var model = Assert.IsType<DistributionPreviousListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(expectedOrder, string.Join(",", model.Distributions.Select(d => d.DistributionReference)));
    }

    private static DistributionResultsEntryViewModel EntryModel(Guid schemeId) => new()
    {
        MonthlyDistributionSchemeId = schemeId,
        Heading = "D26-01A/BA PT001: Bacteriology",
        Tests =
        [
            new DistributionResultsEntryTestViewModel
            {
                TestId = Guid.NewGuid(),
                TestName = "Bacteriology",
                MethodItems = [new DistributionResultsEntryMethodItemViewModel { TestMethodItemId = Guid.NewGuid(), Name = "Method", FormatString = "^.*$", ReturnValueType = "Text", Result = "PCR" }],
                Samples =
                [
                    new DistributionResultsEntrySampleRowViewModel
                    {
                        SampleId = Guid.NewGuid(),
                        SampleNumber = "S001",
                        Values = [new DistributionResultsEntrySampleValueViewModel { SampleResultItemId = Guid.NewGuid(), TestResultItemId = Guid.NewGuid(), Result = "Negative" }]
                    }
                ]
            }
        ]
    };

    private static ActualResultsResponse OwnResults(Guid schemeId, Guid ownDistributionParticipantId) =>
        new(ownDistributionParticipantId, schemeId, "D26-01A/BA PT001: Bacteriology", "audit", new DateTime(2026, 4, 30), false, []);

    [Fact]
    public async Task Entry_Post_SuccessfulSave_RedirectsToCurrent()
    {
        var schemeId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = OwnResults(schemeId, Guid.NewGuid()),
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>())
        };

        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, participantId);

        var result = await controller.Entry(EntryModel(schemeId), "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Current), redirect.ActionName);
        Assert.Single(apiClient.SavedActualResultsRequests);
        Assert.False(apiClient.SavedActualResultsRequests[0].Submitted);
    }

    [Fact]
    public async Task Entry_Post_Submit_SendsTheSubmittedFlag()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = OwnResults(schemeId, Guid.NewGuid()),
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>())
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        await controller.Entry(EntryModel(schemeId), "submit", CancellationToken.None);

        Assert.True(Assert.Single(apiClient.SavedActualResultsRequests).Submitted);
    }

    [Fact]
    public async Task Entry_Post_SavesAgainstTheSignedInParticipantsOwnRecord()
    {
        var schemeId = Guid.NewGuid();
        var signedInParticipantId = Guid.NewGuid();
        var ownDistributionParticipantId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = OwnResults(schemeId, ownDistributionParticipantId),
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>())
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, signedInParticipantId);

        await controller.Entry(EntryModel(schemeId), "save", CancellationToken.None);

        Assert.Equal(signedInParticipantId, Assert.Single(apiClient.RequestedOwnResultsParticipantIds));
        Assert.Equal(ownDistributionParticipantId, Assert.Single(apiClient.SavedActualResultsParticipantIds));
    }

    [Fact]
    public async Task Entry_Post_NoRecordForTheSignedInParticipant_SavesNothingAndReturnsToCurrent()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = null };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Entry(EntryModel(Guid.NewGuid()), "save", CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Entry_Get_LoadsTheSignedInParticipantsOwnRecord()
    {
        var schemeId = Guid.NewGuid();
        var signedInParticipantId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = OwnResults(schemeId, Guid.NewGuid()) };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, signedInParticipantId);

        var result = await controller.Entry(schemeId, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(signedInParticipantId, Assert.Single(apiClient.RequestedOwnResultsParticipantIds));
    }

    [Fact]
    public async Task Entry_Get_ReadOnlyRecord_MapsReadOnlyStateToViewModel()
    {
        var schemeId = Guid.NewGuid();
        var signedInParticipantId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = OwnResults(schemeId, Guid.NewGuid()) with { IsReadOnly = true } };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, signedInParticipantId);

        var result = await controller.Entry(schemeId, CancellationToken.None);

        var model = Assert.IsType<DistributionResultsEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.IsReadOnly);
    }

    [Fact]
    public async Task Entry_Get_WithExistingNilReturn_MapsStateToViewModel()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = OwnResults(schemeId, Guid.NewGuid()),
            ParticipantNilReturn = new ParticipantNilReturnResponse(true, "No samples available")
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Entry(schemeId, CancellationToken.None);

        var model = Assert.IsType<DistributionResultsEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.HasNilReturn);
        Assert.Equal("No samples available", model.NilReturnReason);
    }

    [Fact]
    public async Task NilReturn_Get_OpenDistribution_LoadsViewWithExistingReason()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = OwnResults(schemeId, Guid.NewGuid()),
            ParticipantNilReturn = new ParticipantNilReturnResponse(true, "Reason text")
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.NilReturn(schemeId, CancellationToken.None);

        var model = Assert.IsType<DistributionNilReturnViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(schemeId, model.MonthlyDistributionSchemeId);
        Assert.True(model.HasNilReturn);
        Assert.Equal("Reason text", model.NilReturnReason);
    }

    [Fact]
    public async Task NilReturn_Post_Success_RedirectsToEntry_AndSavesReason()
    {
        var schemeId = Guid.NewGuid();
        var ownParticipantId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = OwnResults(schemeId, ownParticipantId),
            ParticipantNilReturnSaveResult = new ParticipantNilReturnSaveResult(true)
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.NilReturn(new DistributionNilReturnViewModel
        {
            MonthlyDistributionSchemeId = schemeId,
            NilReturnReason = "No samples received"
        }, "submit", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Entry), redirect.ActionName);
        Assert.Equal(schemeId, redirect.RouteValues!["schemeId"]);

        var saved = Assert.Single(apiClient.SavedParticipantNilReturns);
        Assert.Equal(schemeId, saved.SchemeId);
        Assert.Equal(ownParticipantId, saved.DistributionParticipantId);
        Assert.Equal("No samples received", saved.Request.NilReturnReason);
    }

    [Fact]
    public async Task NilReturn_Post_ReasonRequired_AddsFieldError()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = OwnResults(schemeId, Guid.NewGuid()),
            ParticipantNilReturnSaveResult = new ParticipantNilReturnSaveResult(false, ReasonRequired: true)
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.NilReturn(new DistributionNilReturnViewModel
        {
            MonthlyDistributionSchemeId = schemeId,
            NilReturnReason = ""
        }, "submit", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Enter a reason for nil return.", Assert.Single(controller.ModelState[nameof(DistributionNilReturnViewModel.NilReturnReason)]!.Errors).ErrorMessage);
    }

    private sealed record DropdownFixture(
        Guid SchemeId, Guid ResultItemId, Guid SampleValueId, Guid MethodItemId, ActualResultsResponse Own);

    private static DropdownFixture DropdownResults(bool isReadOnly = false)
    {
        var schemeId = Guid.NewGuid();
        var resultItemId = Guid.NewGuid();
        var sampleValueId = Guid.NewGuid();
        var methodItemId = Guid.NewGuid();
        var own = new ActualResultsResponse(
            Guid.NewGuid(), schemeId, "D26-01A/BA PT001: Bacteriology", "audit", new DateTime(2026, 4, 30), false,
            [new ActualResultsTestResponse(
                Guid.NewGuid(),
                "ELISA",
                [new ActualResultsResultItemResponse(resultItemId, "Result", "Positive|Negative", "Text")],
                [new ActualResultsMethodItemResponse(methodItemId, "Kit", "A|B", "Text", "")],
                [new ActualResultsSampleRowResponse(Guid.NewGuid(), "26/7001", [new ActualResultsSampleValueResponse(sampleValueId, resultItemId, "")])])],
            isReadOnly);
        return new DropdownFixture(schemeId, resultItemId, sampleValueId, methodItemId, own);
    }

    private static DistributionResultsEntryViewModel PostedDropdowns(DropdownFixture f, string? sampleResult, string? methodResult) => new()
    {
        MonthlyDistributionSchemeId = f.SchemeId,
        Tests =
        [
            new DistributionResultsEntryTestViewModel
            {
                TestId = Guid.NewGuid(),
                MethodItems = [new DistributionResultsEntryMethodItemViewModel { TestMethodItemId = f.MethodItemId, Result = methodResult }],
                Samples =
                [
                    new DistributionResultsEntrySampleRowViewModel
                    {
                        Values = [new DistributionResultsEntrySampleValueViewModel { SampleResultItemId = f.SampleValueId, TestResultItemId = f.ResultItemId, Result = sampleResult }]
                    }
                ]
            }
        ]
    };

    [Fact]
    public async Task Entry_Post_DeadlinePassedOnTheOwnRecord_ShowsSaveFailedFirstAndNeverCallsTheSave()
    {
        var f = DropdownResults(isReadOnly: true);
        var apiClient = new FakeDistributionApiClient { ActualResults = f.Own };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Entry(PostedDropdowns(f, "Rubbish", "Rubbish"), "save", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(true, view.ViewData["SaveFailed"]);
        Assert.Empty(apiClient.SavedActualResultsRequests);
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Entry_Post_DropdownValueNotInItsOptions_IsRejectedOnTheFieldAndNothingIsSaved()
    {
        var f = DropdownResults();
        var apiClient = new FakeDistributionApiClient { ActualResults = f.Own };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Entry(PostedDropdowns(f, "Maybe", "C"), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Empty(apiClient.SavedActualResultsRequests);
        Assert.Equal("Result must be one of the listed options", Assert.Single(controller.ModelState["Tests[0].Samples[0].Values[0].Result"]!.Errors).ErrorMessage);
        Assert.Equal("Kit must be one of the listed options", Assert.Single(controller.ModelState["Tests[0].MethodItems[0].Result"]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("Positive", "A")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public async Task Entry_Post_ListedOrBlankDropdownValues_AreSaved(string? sampleResult, string? methodResult)
    {
        var f = DropdownResults();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = f.Own,
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>())
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Entry(PostedDropdowns(f, sampleResult, methodResult), "save", CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Single(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Entry_Post_Cancel_ReturnsToCurrentWithoutLookingUpOrSavingAnything()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = OwnResults(Guid.NewGuid(), Guid.NewGuid()) };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Entry(EntryModel(Guid.NewGuid()), "cancel", CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(apiClient.RequestedOwnResultsParticipantIds);
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Entry_Post_InvalidModelState_ShowsThePostedEntryAndSavesNothing()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = OwnResults(schemeId, Guid.NewGuid()) };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());
        controller.ModelState.AddModelError("Tests[0].MethodItems[0].Result", "Bad");
        var posted = EntryModel(schemeId);

        var result = await controller.Entry(posted, "save", CancellationToken.None);

        Assert.Same(posted, Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Entry_Post_SaveEndpointReturnsNothing_ShowsTheEntryAgain()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = OwnResults(schemeId, Guid.NewGuid()), ActualResultsSaveResult = null };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Entry(EntryModel(schemeId), "save", CancellationToken.None);

        Assert.IsType<DistributionResultsEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Single(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Entry_Post_NoSchemeIdPosted_ReturnsToCurrentWithoutLookingUpOrSavingAnything()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = OwnResults(Guid.NewGuid(), Guid.NewGuid()) };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());
        var posted = EntryModel(Guid.NewGuid());
        posted.MonthlyDistributionSchemeId = null;

        var result = await controller.Entry(posted, "save", CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(apiClient.RequestedOwnResultsParticipantIds);
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Results_NoSignedInParticipant_IsNotFound()
    {
        var controller = new DistributionController(new FakeDistributionApiClient())
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        Assert.IsType<NotFoundResult>(await controller.Results(Guid.NewGuid(), null, CancellationToken.None));
    }

    [Theory]
    [InlineData("schemeCode", "asc", "B,A,C")]
    [InlineData("schemeCode", "desc", "C,A,B")]
    [InlineData("schemeName", "asc", "A,C,B")]
    [InlineData("deadlineDate", "asc", "C,B,A")]
    [InlineData("distributionDate", "asc", "A,C,B")]
    public async Task Current_SortsByEachColumn(string sortBy, string sortDir, string expectedOrder)
    {
        CurrentDistributionResponse Row(string reference, string code, string name, int distributionDay, int deadlineDay) => new(
            Guid.NewGuid(), Guid.NewGuid(), reference, code, name,
            new DateTime(2026, 4, distributionDay), new DateTime(2026, 5, deadlineDay), HasResults: false, Submitted: false);

        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions = [Row("A", "PT2", "Alpha", 1, 30), Row("B", "PT1", "Charlie", 9, 20), Row("C", "PT3", "Bravo", 5, 10)]
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Current(sortBy, sortDir);

        var model = Assert.IsType<CurrentDistributionsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(expectedOrder, string.Join(",", model.Distributions.Select(d => d.DistributionReference)));
    }

    [Fact]
    public void ResultsEntryMethodItemViewModel_DropdownAndFormatProperties()
    {
        var dropdown = new DistributionResultsEntryMethodItemViewModel { FormatString = "A|B", ReturnValueType = "Text" };
        var date = new DistributionResultsEntryMethodItemViewModel { FormatString = "^.*$", ReturnValueType = "Date of testing" };

        Assert.True(dropdown.IsDropdown);
        Assert.Equal(["A", "B"], dropdown.DropdownOptions);
        Assert.Equal("Input must be of format: Text", dropdown.InputFormatMessage);
        Assert.False(date.IsDropdown);
        Assert.Empty(date.DropdownOptions);
        Assert.Equal("dd/mm/yyyy", date.RequiredFormat);
        Assert.Equal("Drop down list", dropdown.RequiredFormatHint);
        Assert.Equal("dd/mm/yyyy", date.RequiredFormatHint);
    }

    [Fact]
    public async Task Entry_Post_DeadlinePassed_ShowsTheEntryAgainWithSaveFailedAndDoesNotReturnToCurrent()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = OwnResults(schemeId, Guid.NewGuid()),
            ActualResultsSaveResult = new ActualResultsSaveResult(false, new Dictionary<Guid, string>(), DeadlinePassed: true)
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Entry(EntryModel(schemeId), "save", CancellationToken.None);

        Assert.Equal(true, Assert.IsType<ViewResult>(result).ViewData["SaveFailed"]);
    }

    [Fact]
    public async Task Entry_Post_ApiFormatErrors_AreShownInTheLegacyWordingNamingTheItem()
    {
        var schemeId = Guid.NewGuid();
        var resultItemId = Guid.NewGuid();
        var sampleValueId = Guid.NewGuid();
        var methodItemId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();
        var page = new ActualResultsResponse(
            Guid.NewGuid(), schemeId, "D26-01A/BA PT001: Bacteriology", "audit", new DateTime(2026, 4, 30), false,
            [new ActualResultsTestResponse(
                Guid.NewGuid(),
                "ELISA",
                [new ActualResultsResultItemResponse(resultItemId, "PI Values", "^.*$", "X(15)")],
                [new ActualResultsMethodItemResponse(methodItemId, "Kit", "^.*$", "X(10)", "")],
                [new ActualResultsSampleRowResponse(Guid.NewGuid(), "26/7001", [new ActualResultsSampleValueResponse(sampleValueId, resultItemId, "")])])]);
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = page,
            ActualResultsSaveResult = new ActualResultsSaveResult(false, new Dictionary<Guid, string>
            {
                [sampleValueId] = "Input must be of format: X(15)",
                [methodItemId] = "Input must be of format: X(10)",
                [otherItemId] = "Some other problem",
            })
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        await controller.Entry(EntryModel(schemeId), "save", CancellationToken.None);

        string Message(string key) => Assert.Single(controller.ModelState[key]!.Errors).ErrorMessage;
        Assert.Equal("PI Values must be in format X(15)", Message("Tests[0].Samples[0].Values[0].Result"));
        Assert.Equal("Kit must be in format X(10)", Message("Tests[0].MethodItems[0].Result"));
        Assert.Equal("Some other problem", Message(string.Empty));
    }

    [Fact]
    public async Task Entry_Post_ApiDateFormatError_IsShownInLowercaseNamingTheDateItem()
    {
        var schemeId = Guid.NewGuid();
        var methodItemId = Guid.NewGuid();
        var page = new ActualResultsResponse(
            Guid.NewGuid(), schemeId, "D26-01A/BA PT001: Bacteriology", "audit", new DateTime(2026, 4, 30), false,
            [new ActualResultsTestResponse(
                Guid.NewGuid(),
                "ELISA",
                [],
                [new ActualResultsMethodItemResponse(methodItemId, "Date of Testing", "^.*$", "Date of testing", "")],
                [])]);
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = page,
            ActualResultsSaveResult = new ActualResultsSaveResult(false, new Dictionary<Guid, string> { [methodItemId] = "Input must be of format: dd/MM/yyyy" })
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        await controller.Entry(EntryModel(schemeId), "save", CancellationToken.None);

        Assert.Equal("Date of Testing must be in format dd/mm/yyyy", Assert.Single(controller.ModelState["Tests[0].MethodItems[0].Result"]!.Errors).ErrorMessage);
    }

    [Fact]
    public void ResultEntryField_ForAnErroredKey_StylesTheFieldAndLinksTheMessage()
    {
        const string key = "Tests[0].Samples[1].Values[2].Result";
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        modelState.AddModelError(key, "PI Values must be in format X(15)");

        var field = PTL.ExternalWeb.ViewSupport.ResultEntryField.For(modelState, key);

        Assert.True(field.Invalid);
        Assert.Equal("PI Values must be in format X(15)", field.Message);
        Assert.Equal("Tests_0__Samples_1__Values_2__Result-error", field.ErrorId);
        Assert.Equal(field.ErrorId, field.DescribedBy);
        Assert.Contains("govuk-form-group--error", field.GroupClass, StringComparison.Ordinal);
        Assert.Equal("govuk-input govuk-input--error", field.InputClass);
        Assert.Equal("govuk-select govuk-select--error", field.SelectClass);
    }

    [Fact]
    public void ResultEntryField_ForAKeyWithoutErrors_KeepsTheDefaultStyling()
    {
        var field = PTL.ExternalWeb.ViewSupport.ResultEntryField.For(new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary(), "Tests[0].MethodItems[0].Result");

        Assert.False(field.Invalid);
        Assert.Null(field.Message);
        Assert.Null(field.DescribedBy);
        Assert.DoesNotContain("error", field.GroupClass + field.InputClass + field.SelectClass, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 15, 15, 15, 1)]
    [InlineData(2, 15, 15, 5, 2)]
    [InlineData(0, 15, 15, 15, 1)]
    [InlineData(1, 7, 15, 15, 1)]
    [InlineData(1, 10, 10, 10, 1)]
    public async Task Current_PagesTheListAtFifteenByDefault(int page, int pageSize, int expectedPageSize, int expectedRows, int expectedPage)
    {
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions = [.. Enumerable.Range(1, 20).Select(i => new CurrentDistributionResponse(
                Guid.NewGuid(), Guid.NewGuid(), $"D{i:D2}", "PT001", "Bacteriology",
                new DateTime(2026, 4, 3), new DateTime(2026, 4, 30), HasResults: false, Submitted: false))]
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Current(page: page, pageSize: pageSize);

        var model = Assert.IsType<CurrentDistributionsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(20, model.TotalCount);
        Assert.Equal(expectedPageSize, model.PageSize);
        Assert.Equal(expectedPage, model.Page);
        Assert.Equal(expectedRows, model.Distributions.Count);
    }

    [Fact]
    public async Task Current_SortsTheWholeListBeforeTakingThePage()
    {
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions = [.. Enumerable.Range(1, 20).Select(i => new CurrentDistributionResponse(
                Guid.NewGuid(), Guid.NewGuid(), $"D{i:D2}", "PT001", "Bacteriology",
                new DateTime(2026, 4, 3), new DateTime(2026, 4, 30), HasResults: false, Submitted: false))]
        };
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Current("reference", "desc", page: 2, pageSize: 15);

        var model = Assert.IsType<CurrentDistributionsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(["D05", "D04", "D03", "D02", "D01"], model.Distributions.Select(d => d.DistributionReference));
    }

    [Fact]
    public void ResultsEntryResultItemViewModel_DateReturnValueType_ShowsDdMmYyyyHint()
    {
        var item = new DistributionResultsEntryResultItemViewModel
        {
            ReturnValueType = "Date of testing",
            FormatString = "^.*$"
        };

        Assert.Equal("Input must be of format: dd/mm/yyyy", item.InputFormatMessage);
    }
}
