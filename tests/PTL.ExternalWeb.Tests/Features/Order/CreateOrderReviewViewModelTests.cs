using PTL.Contracts.CreateOrder;
using PTL.ExternalWeb.Features.Order;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Order;

public class CreateOrderReviewViewModelTests
{
    private static readonly Guid ConsentedScheme = Guid.NewGuid();
    private static readonly Guid OtherScheme = Guid.NewGuid();

    private static CreateOrderResponse Order() => FakeCreateOrderApiClient.SampleOrder(selected: true) with
    {
        PurchaseOrderNumber = "PO-1",
        Consents =
        [
            new CreateOrderConsentResponse(ConsentedScheme, "PT1", "Alpha", "Share", Given: true),
            new CreateOrderConsentResponse(OtherScheme, "PT2", "Beta", "Share", Given: false)
        ]
    };

    [Fact]
    public void ANewPage_StartsFromTheSavedPurchaseOrderAndConsents()
    {
        var model = new CreateOrderReviewViewModel("current", Order());

        Assert.Equal("PO-1", model.PurchaseOrderNumber);
        Assert.False(model.TermsAccepted);
        Assert.True(model.HasConsents);
        Assert.True(model.IsConsented(ConsentedScheme));
        Assert.False(model.IsConsented(OtherScheme));
        Assert.Empty(model.Errors);
        Assert.Empty(model.ErrorSummary);
    }

    [Fact]
    public void AnOrderWithoutConsents_HasNoConsentSection()
    {
        var model = new CreateOrderReviewViewModel("current", FakeCreateOrderApiClient.SampleOrder(selected: true));

        Assert.False(model.HasConsents);
        Assert.Equal(string.Empty, model.PurchaseOrderNumber);
    }

    [Fact]
    public void ErrorSummary_FollowsThePageOrderAndErrorForFindsTheFieldMessage()
    {
        var model = new CreateOrderReviewViewModel("current", Order())
        {
            Errors =
            [
                new CreateOrderValidationErrorResponse("TermsAccepted", "terms"),
                new CreateOrderValidationErrorResponse("DataConsent", "consent"),
                new CreateOrderValidationErrorResponse("PurchaseOrderNumber", "po"),
                new CreateOrderValidationErrorResponse("Schemes", "schemes")
            ]
        };

        Assert.Equal(["Schemes", "PurchaseOrderNumber", "DataConsent", "TermsAccepted"], model.ErrorSummary.Select(e => e.Field));
        Assert.Equal("po", model.ErrorFor("PurchaseOrderNumber"));
        Assert.Null(model.ErrorFor("Unknown"));
    }

    [Theory]
    [InlineData("https://example.test/terms.pdf", "https://example.test/terms.pdf")]
    [InlineData("  http://example.test/terms  ", "http://example.test/terms")]
    [InlineData("ftp://example.test/terms", null)]
    [InlineData("/relative/terms", "/relative/terms")]
    [InlineData("//evil.test/terms", null)]
    [InlineData("/\\evil.test/terms", null)]
    [InlineData("/", null)]
    [InlineData("relative/terms", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TermsAndConditionsUrl_OnlyAWebAddressOrAPathOnThisSiteIsUsed(string? configured, string? expected)
    {
        var options = new OrderPageOptions { TermsAndConditionsUrl = configured };

        Assert.Equal(expected, options.SafeTermsAndConditionsUrl);
    }

    [Fact]
    public void Defaults_PointAtTheShippedTermsPdfAndTheVetqasTimetablePage()
    {
        var options = new OrderPageOptions();

        Assert.Equal("/documents/VETQAS%20Ts%20%26%20Cs%20April%202025%20Version%206%201.pdf", options.SafeTermsAndConditionsUrl);
        Assert.Equal("https://www.aphascientific.com/services/vetqas-proficiency-testing/", options.SafeDistributionTimetableUrl);
    }

    [Theory]
    [InlineData("https://example.test/prices", "https://example.test/prices")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("/relative", null)]
    [InlineData(null, null)]
    public void DistributionTimetableUrl_OnlyAnAbsoluteWebAddressIsUsed(string? configured, string? expected)
    {
        var options = new OrderPageOptions { DistributionTimetableUrl = configured };

        Assert.Equal(expected, options.SafeDistributionTimetableUrl);
    }
}
