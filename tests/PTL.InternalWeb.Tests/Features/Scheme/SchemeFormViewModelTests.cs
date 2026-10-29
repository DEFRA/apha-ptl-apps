using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Tests.Features.Scheme;

// SchemeFormViewModel.Validate() (ValidateDistribution/ValidateDataConsentDeclaration) is only
// ever invoked by ASP.NET Core's model-validation pipeline, never by the controller directly - the
// existing SchemeControllerTests bypass real model binding, so this is the only place these two
// rules get exercised.
public class SchemeFormViewModelTests
{
    private static List<ValidationResult> Validate(PTL.InternalWeb.Features.Scheme.SchemeFormViewModel model) =>
        model.Validate(new ValidationContext(model)).ToList();

    [Fact]
    public void Validate_NoMonthsSelectedAndNotAsAvailable_ReturnsDistributionError()
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { DistributionAsAvailable = false };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.DistributionAsAvailable)));
    }

    [Fact]
    public void Validate_MonthSelectedAndAsAvailable_ReturnsDistributionError()
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { DistributionMonthApr = true, DistributionAsAvailable = true };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.DistributionAsAvailable)));
    }

    [Fact]
    public void Validate_MonthSelectedAndNotAsAvailable_NoDistributionError()
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { DistributionMonthApr = true, DistributionAsAvailable = false };

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.DistributionAsAvailable)));
    }

    [Fact]
    public void Validate_ConsentActiveWithoutText_ReturnsConsentError()
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel
        {
            DistributionMonthApr = true,
            DataConsentDeclarationActive = true,
            DataConsentDeclarationText = null
        };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.DataConsentDeclarationText)));
    }

    [Fact]
    public void Validate_ConsentActiveWithText_NoConsentError()
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel
        {
            DistributionMonthApr = true,
            DataConsentDeclarationActive = true,
            DataConsentDeclarationText = "I consent to this."
        };

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.DataConsentDeclarationText)));
    }

    [Fact]
    public void Validate_InstructionsRendersAsEmpty_ReturnsInstructionsError()
    {
        // An empty paragraph passes [Required] (the markup itself is non-empty) but legacy
        // measured the visible text, so this must still be rejected.
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel
        {
            DistributionMonthApr = true,
            Instructions = "<p></p>"
        };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Instructions)));
    }

    [Fact]
    public void Validate_InstructionsHasVisibleText_NoInstructionsError()
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel
        {
            DistributionMonthApr = true,
            Instructions = "<p>Follow the packing instructions.</p>"
        };

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.Instructions)));
    }

    [Fact]
    public void Validate_FewerThanTwoTabulationsOnNonAssessmentScheme_ForwardsTheTabulationsError()
    {
        // SchemeValidator's "Tabulations" error is a forwarded domain field, so it must surface on
        // the view model even though no single input on the form owns it.
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel
        {
            DistributionMonthApr = true,
            RequiresAssessment = false,
        };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains("Tabulations"));
    }

    [Fact]
    public void Validate_SampleOriginMissing_DoesNotSurfaceAsAFormViewModelError()
    {
        // SampleOrigin already has its own [Required] attribute on the view model, so
        // SchemeValidator's identical domain-level error is not a forwarded field and must be
        // filtered out here rather than duplicated.
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel
        {
            DistributionMonthApr = true,
            SampleOrigin = null,
        };

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.SampleOrigin)));
    }

    private const string ReasonRequiredMessage = "The reason for these changes must be specified";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A reason")]
    public void Validate_NotOnTheAuditStep_NeverAsksForAReason(string? reason)
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { DistributionMonthApr = true, IsAuditStep = false, AuditWhy = reason };

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.AuditWhy)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Validate_OnTheAuditStepWithoutAReason_RequiresOne(string? reason)
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { DistributionMonthApr = true, IsAuditStep = true, AuditWhy = reason };

        var errors = Validate(model);

        var error = Assert.Single(errors, e => e.MemberNames.Contains(nameof(model.AuditWhy)));
        Assert.Equal(ReasonRequiredMessage, error.ErrorMessage);
    }

    [Fact]
    public void Validate_OnTheAuditStepWithAReason_AcceptsIt()
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { DistributionMonthApr = true, IsAuditStep = true, AuditWhy = "Corrected the order" };

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.AuditWhy)));
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void AuditWhy_IsLimitedToTwoThousandCharacters(int length, bool valid)
    {
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { AuditWhy = new string('x', length) };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateProperty(
            model.AuditWhy, new ValidationContext(model) { MemberName = nameof(model.AuditWhy) }, results);

        Assert.Equal(valid, isValid);
        if (!valid)
        {
            Assert.Equal("Input can not exceed 2000 characters", Assert.Single(results).ErrorMessage);
        }
    }

    [Fact]
    public void CheckboxId_ReplacesIndexerAndPropertyPunctuationWithUnderscores()
    {
        var itemId = Guid.NewGuid();
        var checkbox = new PTL.InternalWeb.Features.Scheme.SchemeTabulationItemCheckboxViewModel(
            FieldName: "Tabulations[0].ResultItemIds",
            ItemId: itemId,
            Selected: true,
            Label: "Titre in Published");

        Assert.Equal($"Tabulations_0__ResultItemIds{itemId:N}", checkbox.CheckboxId);
    }
}
