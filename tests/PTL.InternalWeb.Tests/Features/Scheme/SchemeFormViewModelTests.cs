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
}
