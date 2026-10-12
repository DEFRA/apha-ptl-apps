using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using PTL.InternalWeb.Features.Distribution;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// SampleNumbersViewModel repeats the legacy CSLA required/max-length rules as DataAnnotations so
// the user gets GOV.UK field-level errors without a round trip - the message text must stay
// identical to PTL.Core's SampleNumbersValidator. The computed display properties are only read by
// the Razor view, so each needs a direct read here.
public class SampleNumbersViewModelTests
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

    private static SampleNumberRowViewModel Row() => new()
    {
        SampleId = Guid.NewGuid(),
        SampleNumber = "S1",
        InternalDescription = "Internal",
    };

    [Fact]
    public void DistributionHeading_CombinesReferenceIdentifierAndName()
    {
        var model = new SampleNumbersViewModel
        {
            DistributionReferenceFull = "17152A/BA",
            SchemeIdentifier = "PT0060",
            SchemeName = "Mastitis bovine",
        };

        Assert.Equal("17152A/BA PT0060: Mastitis bovine", model.DistributionHeading);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public void AllocationEditable_OnlyLockedOnceAnAssessmentSchemeIsReleased(bool requiresAssessment, bool isReadyForAssessment, bool expected)
    {
        var model = new SampleNumbersViewModel { RequiresAssessment = requiresAssessment, IsReadyForAssessment = isReadyForAssessment };

        Assert.Equal(expected, model.AllocationEditable);
    }

    [Fact]
    public void AllocationIndexFor_ReturnsIndexOfMatchingTest()
    {
        var firstTestId = Guid.NewGuid();
        var secondTestId = Guid.NewGuid();
        var row = Row();
        row.Allocations =
        [
            new SampleAllocationViewModel { SampleTestId = Guid.NewGuid(), TestId = firstTestId },
            new SampleAllocationViewModel { SampleTestId = Guid.NewGuid(), TestId = secondTestId },
        ];
        var model = new SampleNumbersViewModel { Samples = [row] };

        Assert.Equal(0, model.AllocationIndexFor(0, firstTestId));
        Assert.Equal(1, model.AllocationIndexFor(0, secondTestId));
    }

    [Fact]
    public void AllocationIndexFor_SamplePairWithNoSampleTestRow_ReturnsMinusOne()
    {
        var model = new SampleNumbersViewModel { Samples = [Row()] };

        Assert.Equal(-1, model.AllocationIndexFor(0, Guid.NewGuid()));
    }

    [Fact]
    public void RoundTrippedProperties_AreReadable()
    {
        var schemeId = Guid.NewGuid();
        var sampleTestId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var model = new SampleNumbersViewModel
        {
            MonthlyDistributionSchemeId = schemeId,
            YearId = 2026,
            MonthId = 4,
            NumberOfSetsRequired = 9,
            Comments = "Comments",
        };
        var allocation = new SampleAllocationViewModel { SampleTestId = sampleTestId, TestId = testId, IsApplicable = true };
        var test = new SampleNumbersTestViewModel
        {
            TestId = testId,
            TestName = "Somatic cell count",
            TestResultItemNames = ["Result A"],
            TestMethodItemNames = ["Method A"],
            CategoryItemNames = ["Appearance ()"],
        };

        Assert.Equal(schemeId, model.MonthlyDistributionSchemeId);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
        Assert.Equal(9, model.NumberOfSetsRequired);
        Assert.Equal("Comments", model.Comments);
        Assert.Equal(sampleTestId, allocation.SampleTestId);
        Assert.Equal(testId, allocation.TestId);
        Assert.True(allocation.IsApplicable);
        Assert.Equal(testId, test.TestId);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(["Result A"], test.TestResultItemNames);
        Assert.Equal(["Method A"], test.TestMethodItemNames);
        Assert.Equal(["Appearance ()"], test.CategoryItemNames);
    }

    [Fact]
    public void Validate_CommentsAtLimit_IsValid()
    {
        var model = new SampleNumbersViewModel { Comments = new string('x', 8000) };

        Assert.Empty(ValidateProperties(model));
    }

    [Fact]
    public void Validate_CommentsOneOverLimit_ReportsLegacyMessage()
    {
        var model = new SampleNumbersViewModel { Comments = new string('x', 8001) };

        var error = Assert.Single(ValidateProperties(model));
        Assert.Equal("Comments must not exceed 8000 characters", error.ErrorMessage);
    }

    [Fact]
    public void Validate_SampleRowWithinLimits_IsValid()
    {
        Assert.Empty(ValidateProperties(Row()));
    }

    // Regression guard: these four fields have NO RequiredFieldValidator on legacy
    // SampleNumbers.aspx. Declaring any of them as a non-nullable string would make ASP.NET Core
    // treat it as implicitly required and emit "The X field is required.", which is exactly the
    // behaviour this screen must not have.
    [Fact]
    public void Validate_EmptyOptionalSampleFields_AreNotRequired()
    {
        var row = Row();
        row.TabulationDescription = null;
        row.ExternalDescription = null;
        row.TestConsultantsDescription = null;

        Assert.Empty(ValidateProperties(row));
    }

    [Fact]
    public void Validate_EmptyComments_IsNotRequired()
    {
        Assert.Empty(ValidateProperties(new SampleNumbersViewModel { Comments = null }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingSampleNumber_ReportsLegacyRequiredMessage(string sampleNumber)
    {
        var row = Row();
        row.SampleNumber = sampleNumber;

        var errors = ValidateProperties(row);

        Assert.Contains(errors, e => e.ErrorMessage == "Enter the Sample Number");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingInternalDescription_ReportsLegacyRequiredMessage(string internalDescription)
    {
        var row = Row();
        row.InternalDescription = internalDescription;

        var errors = ValidateProperties(row);

        Assert.Contains(errors, e => e.ErrorMessage == "Enter the Internal Description");
    }

    [Fact]
    public void Validate_SampleNumberOneOverLimit_ReportsLegacyMaxLengthMessage()
    {
        var row = Row();
        row.SampleNumber = new string('x', 51);

        Assert.Contains(ValidateProperties(row), e => e.ErrorMessage == "Sample Number must not exceed 50 characters");
    }

    [Fact]
    public void Validate_InternalDescriptionOneOverLimit_ReportsLegacyMaxLengthMessage()
    {
        var row = Row();
        row.InternalDescription = new string('x', 101);

        Assert.Contains(ValidateProperties(row), e => e.ErrorMessage == "Internal Description must not exceed 100 characters");
    }

    [Fact]
    public void Validate_DescriptionsAtLimit_AreValid()
    {
        var row = Row();
        row.ExternalDescription = new string('x', 500);
        row.TabulationDescription = new string('x', 500);
        row.TestConsultantsDescription = new string('x', 500);

        Assert.Empty(ValidateProperties(row));
    }

    [Fact]
    public void Validate_DescriptionsOneOverLimit_ReportLegacyMaxLengthMessages()
    {
        var row = Row();
        row.ExternalDescription = new string('x', 501);
        row.TabulationDescription = new string('x', 501);
        row.TestConsultantsDescription = new string('x', 501);

        var errors = ValidateProperties(row);

        Assert.Contains(errors, e => e.ErrorMessage == "Test Instruction must not exceed 500 characters");
        Assert.Contains(errors, e => e.ErrorMessage == "Tabulation Description must not exceed 500 characters");
        Assert.Contains(errors, e => e.ErrorMessage == "Description for TC must not exceed 500 characters");
    }

    // Legacy parity: SampleNumbers.aspx has a RequiredFieldValidator on Sample No. and Internal
    // Description only, so leaving every other field on the screen blank must still validate.
    [Fact]
    public void Validate_OnlySampleNumberAndInternalDescriptionAreRequired()
    {
        var row = Row();
        row.ExternalDescription = string.Empty;
        row.TabulationDescription = string.Empty;
        row.TestConsultantsDescription = string.Empty;
        var model = new SampleNumbersViewModel { Comments = string.Empty, Samples = [row] };

        Assert.Empty(ValidateProperties(row));
        Assert.Empty(ValidateObject(model));
    }

    // Legacy Audit.aspx's ValidatorAuditWhyRequired only exists on the audit page itself.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_AuditStepShowingWithoutAReason_ReportsLegacyRequiredMessage(string? auditWhy)
    {
        var model = new SampleNumbersViewModel { RequiresAuditReason = true, AuditWhy = auditWhy };

        var error = Assert.Single(ValidateObject(model));
        Assert.Equal("The reason for these changes must be specified", error.ErrorMessage);
        Assert.Equal(nameof(SampleNumbersViewModel.AuditWhy), Assert.Single(error.MemberNames));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_AuditStepNotShowing_BlankReasonIsAllowed(string? auditWhy)
    {
        var model = new SampleNumbersViewModel { RequiresAuditReason = false, AuditWhy = auditWhy };

        Assert.Empty(ValidateObject(model));
    }

    [Fact]
    public void Validate_AuditStepShowingWithAReason_IsValid()
    {
        var model = new SampleNumbersViewModel { RequiresAuditReason = true, AuditWhy = "Corrected a typo" };

        Assert.Empty(ValidateObject(model));
    }

    [Fact]
    public void Validate_AuditReasonAtLimit_IsValid()
    {
        var model = new SampleNumbersViewModel { RequiresAuditReason = true, AuditWhy = new string('x', 2000) };

        Assert.Empty(ValidateObject(model));
    }

    [Fact]
    public void Validate_AuditReasonOneOverLimit_ReportsLegacyMaxLengthMessage()
    {
        var model = new SampleNumbersViewModel { RequiresAuditReason = true, AuditWhy = new string('x', 2001) };

        var error = Assert.Single(ValidateObject(model));
        Assert.Equal("Input can not exceed 2000 characters", error.ErrorMessage);
    }

    [Fact]
    public void AuditRoundTrippedProperties_AreReadable()
    {
        var model = new SampleNumbersViewModel
        {
            RequiresAuditReason = true,
            AuditObjectName = "Intended Results Set",
            PendingCommand = "saveAndGoToIntendedResults",
            AuditWhy = "Corrected a typo",
        };

        Assert.True(model.RequiresAuditReason);
        Assert.Equal("Intended Results Set", model.AuditObjectName);
        Assert.Equal("saveAndGoToIntendedResults", model.PendingCommand);
        Assert.Equal("Corrected a typo", model.AuditWhy);
    }
}
