using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using PTL.InternalWeb.Features.Distribution;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// IntendedResultsViewModel ports legacy IntendedResults.aspx(.vb), which has NO
// RequiredFieldValidator or StringLength validator anywhere on the Result Item/Method Item
// controls - only the audit-reason step (Audit.aspx) has a required rule. The computed display
// properties (IsDropdown/DropdownOptions/ValueIndexFor) are only read by the Razor view, so each
// needs a direct read here.
public class IntendedResultsViewModelTests
{
    private static List<ValidationResult> ValidateProperties(object model)
    {
        var results = new List<ValidationResult>();
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(model))
        {
            var propertyContext = new ValidationContext(model) { MemberName = property.Name };
            Validator.TryValidateProperty(property.GetValue(model), propertyContext, results);
        }

        return results;
    }

    // Property-level attributes plus IValidatableObject.Validate, as MVC model binding runs them.
    private static List<ValidationResult> ValidateObject(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void DistributionHeading_CombinesReferenceIdentifierAndName()
    {
        var model = new IntendedResultsViewModel
        {
            DistributionReferenceFull = "17152A/BA",
            SchemeIdentifier = "PT0060",
            SchemeName = "Mastitis bovine",
        };

        Assert.Equal("17152A/BA PT0060: Mastitis bovine", model.DistributionHeading);
    }

    [Fact]
    public void Validate_IsAuditStepFalse_BlankAuditWhy_IsValid()
    {
        var model = new IntendedResultsViewModel { IsAuditStep = false, AuditWhy = null };

        Assert.Empty(ValidateObject(model));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_IsAuditStepTrue_BlankAuditWhy_ReturnsRequiredMessage(string? auditWhy)
    {
        var model = new IntendedResultsViewModel { IsAuditStep = true, AuditWhy = auditWhy };

        var error = Assert.Single(ValidateObject(model));
        Assert.Equal("The reason for these changes must be specified", error.ErrorMessage);
        Assert.Contains(nameof(IntendedResultsViewModel.AuditWhy), error.MemberNames);
    }

    [Fact]
    public void Validate_IsAuditStepTrue_ReasonSupplied_IsValid()
    {
        var model = new IntendedResultsViewModel { IsAuditStep = true, AuditWhy = "Corrected a typo" };

        Assert.Empty(ValidateObject(model));
    }

    // RequiresAuditReason (the GET-computed, round-tripped gate) must never drive this rule on
    // its own - only an actual audit-step redisplay (IsAuditStep) does.
    [Fact]
    public void Validate_RequiresAuditReasonTrue_ButNotOnTheAuditStep_BlankAuditWhy_IsValid()
    {
        var model = new IntendedResultsViewModel { RequiresAuditReason = true, IsAuditStep = false, AuditWhy = null };

        Assert.Empty(ValidateObject(model));
    }

    [Fact]
    public void Validate_AuditWhyAtLimit_IsValid()
    {
        var model = new IntendedResultsViewModel { AuditWhy = new string('x', 2000) };

        Assert.Empty(ValidateProperties(model));
    }

    [Fact]
    public void Validate_AuditWhyOneOverLimit_ReportsLengthMessage()
    {
        var model = new IntendedResultsViewModel { AuditWhy = new string('x', 2001) };

        var error = Assert.Single(ValidateProperties(model));
        Assert.Equal("Input can not exceed 2000 characters", error.ErrorMessage);
    }

    // Legacy IntendedResults.aspx has no RequiredFieldValidator on the Method Item control.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MethodItem_BlankIntendedResult_IsValid(string? intendedResult)
    {
        var model = new IntendedResultsMethodItemViewModel { TestMethodItemId = Guid.NewGuid(), IntendedResult = intendedResult };

        Assert.Empty(ValidateProperties(model));
    }

    // Legacy IntendedResults.aspx has no RequiredFieldValidator on either Result Item control.
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void SampleValue_BlankValues_AreValid(string? intendedResult, string? intendedResult2)
    {
        var model = new IntendedResultsSampleValueViewModel
        {
            SampleResultItemId = Guid.NewGuid(),
            TestResultItemId = Guid.NewGuid(),
            IntendedResult = intendedResult,
            IntendedResult2 = intendedResult2,
        };

        Assert.Empty(ValidateProperties(model));
    }

    [Theory]
    [InlineData("^[0-9]+$", false)]
    // Legacy's BuildResultCellContents/BuildMethodCellContents is a strict binary branch:
    // `If formatString.StartsWith("^") Then [textbox] Else [dropdown]` - there is no third,
    // free-text case for an empty FormatString, so it still renders as a (practically empty)
    // dropdown with only the "- Select -" placeholder.
    [InlineData("", true)]
    [InlineData("Positive|Negative|Inconclusive", true)]
    public void ResultItem_IsDropdown_FalseOnlyWhenFormatStringIsARegex(string formatString, bool expectedIsDropdown)
    {
        var model = new IntendedResultsResultItemViewModel { TestResultItemId = Guid.NewGuid(), FormatString = formatString };

        Assert.Equal(expectedIsDropdown, model.IsDropdown);
    }

    [Fact]
    public void ResultItem_DropdownOptions_SplitsOnPipeAndRemovesEmptyEntries()
    {
        var model = new IntendedResultsResultItemViewModel { TestResultItemId = Guid.NewGuid(), FormatString = "Positive|Negative||Inconclusive" };

        Assert.Equal(["Positive", "Negative", "Inconclusive"], model.DropdownOptions);
    }

    [Fact]
    public void ResultItem_RegexFormat_HasNoDropdownOptions()
    {
        var model = new IntendedResultsResultItemViewModel { TestResultItemId = Guid.NewGuid(), FormatString = "^[0-9]+$" };

        Assert.Empty(model.DropdownOptions);
    }

    [Theory]
    [InlineData("^[0-9]+$", false)]
    [InlineData("Positive|Negative", true)]
    public void MethodItem_IsDropdown_FalseOnlyWhenFormatStringIsARegex(string formatString, bool expectedIsDropdown)
    {
        var model = new IntendedResultsMethodItemViewModel { TestMethodItemId = Guid.NewGuid(), FormatString = formatString };

        Assert.Equal(expectedIsDropdown, model.IsDropdown);
    }

    [Fact]
    public void MethodItem_DropdownOptions_SplitsOnPipe()
    {
        var model = new IntendedResultsMethodItemViewModel { TestMethodItemId = Guid.NewGuid(), FormatString = "Positive|Negative" };

        Assert.Equal(["Positive", "Negative"], model.DropdownOptions);
    }

    [Fact]
    public void ValueIndexFor_ReturnsIndexOfMatchingResultItem()
    {
        var firstResultItemId = Guid.NewGuid();
        var secondResultItemId = Guid.NewGuid();
        var test = new IntendedResultsTestViewModel
        {
            TestId = Guid.NewGuid(),
            Samples =
            [
                new IntendedResultsSampleRowViewModel
                {
                    SampleId = Guid.NewGuid(),
                    Values =
                    [
                        new IntendedResultsSampleValueViewModel { SampleResultItemId = Guid.NewGuid(), TestResultItemId = firstResultItemId },
                        new IntendedResultsSampleValueViewModel { SampleResultItemId = Guid.NewGuid(), TestResultItemId = secondResultItemId },
                    ],
                },
            ],
        };

        Assert.Equal(0, test.ValueIndexFor(0, firstResultItemId));
        Assert.Equal(1, test.ValueIndexFor(0, secondResultItemId));
    }

    [Fact]
    public void ValueIndexFor_NoMatchingResultItem_ReturnsMinusOne()
    {
        var test = new IntendedResultsTestViewModel
        {
            TestId = Guid.NewGuid(),
            Samples = [new IntendedResultsSampleRowViewModel { SampleId = Guid.NewGuid(), Values = [] }],
        };

        Assert.Equal(-1, test.ValueIndexFor(0, Guid.NewGuid()));
    }

    [Fact]
    public void RoundTrippedProperties_AreReadable()
    {
        var schemeId = Guid.NewGuid();
        var model = new IntendedResultsViewModel
        {
            MonthlyDistributionSchemeId = schemeId,
            YearId = 2026,
            MonthId = 4,
            RequiresAuditReason = true,
            AuditObjectName = "Intended Results Set",
            PendingCommand = "save",
        };

        Assert.Equal(schemeId, model.MonthlyDistributionSchemeId);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
        Assert.True(model.RequiresAuditReason);
        Assert.Equal("Intended Results Set", model.AuditObjectName);
        Assert.Equal("save", model.PendingCommand);
    }
}
