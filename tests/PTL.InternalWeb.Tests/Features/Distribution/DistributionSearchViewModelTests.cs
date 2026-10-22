using System.ComponentModel.DataAnnotations;
using PTL.InternalWeb.Features.Distribution;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Legacy Distributions/Search.aspx - Distribution Number and Sample Number tab forms/view model.
public class DistributionSearchViewModelTests
{
    private static bool TryValidate(object model, out List<ValidationResult> results)
    {
        results = [];
        return Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
    }

    [Fact]
    public void DistributionNumberSearchForm_BlankDistributionNumber_IsValid()
    {
        var form = new DistributionNumberSearchForm();

        Assert.True(TryValidate(form, out var results));
        Assert.Empty(results);
    }

    [Fact]
    public void DistributionNumberSearchForm_OverMaxLength_IsInvalid()
    {
        var form = new DistributionNumberSearchForm { DistributionNumber = new string('x', 51) };

        var valid = TryValidate(form, out var results);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(DistributionNumberSearchForm.DistributionNumber)));
    }

    [Fact]
    public void DistributionNumberSearchForm_AtMaxLength_IsValid()
    {
        var form = new DistributionNumberSearchForm { DistributionNumber = new string('x', 50) };

        Assert.True(TryValidate(form, out _));
    }

    [Fact]
    public void DistributionNumberSearchForm_ToCriteria_PassesTheValueThrough()
    {
        Assert.Equal("D1a", new DistributionNumberSearchForm { DistributionNumber = "D1a" }.ToCriteria().DistributionNumber);
        Assert.Null(new DistributionNumberSearchForm().ToCriteria().DistributionNumber);
    }

    [Fact]
    public void SampleNumberSearchForm_Blank_IsInvalidWithTheExactLegacyMessage()
    {
        var form = new SampleNumberSearchForm();

        var valid = TryValidate(form, out var results);

        Assert.False(valid);
        var error = Assert.Single(results, r => r.MemberNames.Contains(nameof(SampleNumberSearchForm.SampleNumber)));
        Assert.Equal("A Sample Number must be supplied.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SampleNumberSearchForm_NullOrEmpty_FailsRequired(string? value)
    {
        var form = new SampleNumberSearchForm { SampleNumber = value };

        Assert.False(TryValidate(form, out _));
    }

    [Fact]
    public void SampleNumberSearchForm_OverMaxLength_IsInvalid()
    {
        var form = new SampleNumberSearchForm { SampleNumber = new string('x', 51) };

        var valid = TryValidate(form, out var results);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SampleNumberSearchForm.SampleNumber)));
    }

    [Fact]
    public void SampleNumberSearchForm_AtMaxLength_IsValid()
    {
        var form = new SampleNumberSearchForm { SampleNumber = new string('x', 50) };

        Assert.True(TryValidate(form, out _));
    }

    [Fact]
    public void SampleNumberSearchForm_ToCriteria_PassesTheValueThrough()
    {
        Assert.Equal("S00001", new SampleNumberSearchForm { SampleNumber = "S00001" }.ToCriteria().SampleNumber);
    }

    [Fact]
    public void ViewModel_DefaultsToTheDistributionNumberTabWithNoResultsAndFirstPage()
    {
        var model = new DistributionSearchViewModel();

        Assert.Equal(DistributionSearchTab.DistributionNumber, model.ActiveTab);
        Assert.NotNull(model.DistributionNumber);
        Assert.NotNull(model.SampleNumber);
        Assert.Null(model.DistributionNumberResults);
        Assert.Null(model.SampleNumberResults);
        Assert.Equal(0, model.SampleNumberTotalCount);
        Assert.Equal(1, model.SampleNumberPage);
    }

    [Fact]
    public void SampleNumberPageSize_IsFifteen()
    {
        Assert.Equal(15, DistributionSearchViewModel.SampleNumberPageSize);
    }
}
