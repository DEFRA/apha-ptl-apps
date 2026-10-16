using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using Keys = PTL.Contracts.Distribution.AmendResultsFieldKeys;

namespace PTL.InternalWeb.Tests.Features.Distribution;

public class AmendResultsViewModelTests
{
    private static readonly Guid ResultItem = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000f1");

    private static AmendResultsViewModel Model(params (string Key, string Value)[] values) =>
        new()
        {
            YearId = 2026,
            MonthId = 5,
            MonthlyDistributionSchemeId = Guid.Empty,
            Page = DistributionControllerAmendResultsTests.Page(),
            Values = values.ToDictionary(v => v.Key, v => v.Value),
        };

    private static AmendItemResponse Item(bool isRange) => new(ResultItem, "Result", "Pos|Neg", "", isRange);

    [Fact]
    public void Value_NotPosted_ReturnsTheStoredValue()
    {
        Assert.Equal("stored", Model().Value("im_x", "stored"));
    }

    [Theory]
    [InlineData("posted")]
    [InlineData("")]
    public void Value_Posted_ReturnsThePostedValue(string posted)
    {
        Assert.Equal(posted, Model(("im_x", posted)).Value("im_x", "stored"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsRange_NotShownOnThePostedPage_ReturnsTheStoredFlag(bool stored)
    {
        Assert.Equal(stored, Model().IsRange(Item(stored)));
        Assert.Equal(stored, Model((Keys.Range(ResultItem), "true")).IsRange(Item(stored)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsRange_ShownAndTicked_IsTrueWhateverWasStored(bool stored)
    {
        Assert.True(Model((Keys.Range(ResultItem) + "_shown", "1"), (Keys.Range(ResultItem), "true")).IsRange(Item(stored)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsRange_ShownButUnticked_IsFalseWhateverWasStored(bool stored)
    {
        Assert.False(Model((Keys.Range(ResultItem) + "_shown", "1")).IsRange(Item(stored)));
    }

    [Fact]
    public void IsRange_NullItem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Model().IsRange(null!));
    }

    [Fact]
    public void Defaults_AreAnEmptyNonAuditStep()
    {
        var model = Model();

        Assert.False(model.IsAuditStep);
        Assert.Null(model.AuditObjectName);
        Assert.Null(model.PendingCommand);
        Assert.Null(model.AuditWhy);
    }
}
