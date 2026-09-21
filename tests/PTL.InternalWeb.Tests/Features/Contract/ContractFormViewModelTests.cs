using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Tests.Features.Contract;

// ContractFormViewModel.Validate() (ValidateUTFT) is only ever invoked by ASP.NET Core's
// model-validation pipeline, never by the controller directly - existing ContractControllerTests
// bypass real model binding, so this is the only place this rule gets exercised.
public class ContractFormViewModelTests
{
    private static List<ValidationResult> Validate(PTL.InternalWeb.Features.Contract.ContractFormViewModel model) =>
        model.Validate(new ValidationContext(model)).ToList();

    [Fact]
    public void Validate_NeitherUtNorFtNumber_ReturnsError()
    {
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel();

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.UTNumber)));
    }

    [Fact]
    public void Validate_BothUtAndFtNumber_ReturnsError()
    {
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel { UTNumber = "UT1", FTNumber = "FT1" };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.UTNumber)));
    }

    [Fact]
    public void Validate_OnlyUtNumber_NoError()
    {
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel { UTNumber = "UT1" };

        var errors = Validate(model);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_OnlyFtNumber_NoError()
    {
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel { FTNumber = "FT1" };

        var errors = Validate(model);

        Assert.Empty(errors);
    }
}
