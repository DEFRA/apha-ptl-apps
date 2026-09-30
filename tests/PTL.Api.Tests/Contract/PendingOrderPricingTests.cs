using PTL.Core.Contract.PendingOrder;
using PTL.Core.Lookup;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Contract;

// Covers the arithmetic ported from PendingContractOrder.aspx.vb (getTotalSchemePrice,
// getPostageTypeNumbers, getTotalPostagePrice, GetPostagePrice).
public class PendingOrderPricingTests
{
    private static readonly Guid CourierPostageId = Guid.NewGuid();
    private static readonly Guid DryIcePostageId = Guid.NewGuid();
    private static readonly Guid MondayId = Guid.NewGuid();
    private static readonly Guid TuesdayId = Guid.NewGuid();

    private static PostagePricingPlanEntity Plan(string name, Guid postageId) => new()
    {
        PostageId = postageId,
        Name = name,
        UKPrice = 10m,
        EUPrice = 20m,
        NonEUPrice = 30m,
        YearId = 1
    };

    private static CoreScheme Scheme(Guid postageId, bool combinedPackaging, Guid dayOfWeekId, int weekNumber) => new()
    {
        SchemeId = Guid.NewGuid(),
        Postage = postageId,
        CombinedPackaging = combinedPackaging,
        DayOfWeekId = dayOfWeekId,
        WeekNumber = weekNumber
    };

    private static PendingOrderSchemeEntity PendingScheme(Guid schemeId, decimal price, bool isRemoved = false, params int[] months)
    {
        var scheme = new PendingOrderSchemeEntity { PendingParticipantSchemeId = Guid.NewGuid(), SchemeId = schemeId, Price = price, IsRemoved = isRemoved };
        foreach (var month in months)
        {
            switch (month)
            {
                case 1: scheme.DistributionMonthJan = true; break;
                case 2: scheme.DistributionMonthFeb = true; break;
                case 3: scheme.DistributionMonthMar = true; break;
                case 4: scheme.DistributionMonthApr = true; break;
                case 5: scheme.DistributionMonthMay = true; break;
                case 6: scheme.DistributionMonthJun = true; break;
                case 7: scheme.DistributionMonthJul = true; break;
                case 8: scheme.DistributionMonthAug = true; break;
                case 9: scheme.DistributionMonthSep = true; break;
                case 10: scheme.DistributionMonthOct = true; break;
                case 11: scheme.DistributionMonthNov = true; break;
                default: scheme.DistributionMonthDec = true; break;
            }
        }

        return scheme;
    }

    [Fact]
    public void TotalSchemePrice_ExcludesRemovedLines()
    {
        var schemes = new[]
        {
            PendingScheme(Guid.NewGuid(), 100m, false, 4),
            PendingScheme(Guid.NewGuid(), 50m, false, 5),
            PendingScheme(Guid.NewGuid(), 999m, true, 6)
        };

        Assert.Equal(150m, PendingOrderPricing.TotalSchemePrice(schemes));
    }

    [Fact]
    public void PostageTypeNumbers_NonCombinedScheme_CountsEverySelectedMonth()
    {
        var scheme = Scheme(CourierPostageId, combinedPackaging: false, MondayId, 1);
        var realSchemes = new Dictionary<Guid, CoreScheme> { [scheme.SchemeId] = scheme };
        var schemes = new[] { PendingScheme(scheme.SchemeId, 0m, false, 4, 5, 6) };

        Assert.Equal(3, PendingOrderPricing.PostageTypeNumbers(schemes, realSchemes, CourierPostageId));
    }

    [Fact]
    public void PostageTypeNumbers_CombinedSchemesSharingMonthDayAndWeek_CountOnce()
    {
        var first = Scheme(CourierPostageId, combinedPackaging: true, MondayId, 1);
        var second = Scheme(CourierPostageId, combinedPackaging: true, MondayId, 1);
        var realSchemes = new Dictionary<Guid, CoreScheme> { [first.SchemeId] = first, [second.SchemeId] = second };
        var schemes = new[]
        {
            PendingScheme(first.SchemeId, 0m, false, 4, 5),
            PendingScheme(second.SchemeId, 0m, false, 4, 5)
        };

        // Two schemes, same despatch slot, same two months - two despatches, not four.
        Assert.Equal(2, PendingOrderPricing.PostageTypeNumbers(schemes, realSchemes, CourierPostageId));
    }

    [Fact]
    public void PostageTypeNumbers_CombinedSchemesOnDifferentDays_CountSeparately()
    {
        var first = Scheme(CourierPostageId, combinedPackaging: true, MondayId, 1);
        var second = Scheme(CourierPostageId, combinedPackaging: true, TuesdayId, 1);
        var realSchemes = new Dictionary<Guid, CoreScheme> { [first.SchemeId] = first, [second.SchemeId] = second };
        var schemes = new[]
        {
            PendingScheme(first.SchemeId, 0m, false, 4),
            PendingScheme(second.SchemeId, 0m, false, 4)
        };

        Assert.Equal(2, PendingOrderPricing.PostageTypeNumbers(schemes, realSchemes, CourierPostageId));
    }

    [Fact]
    public void PostageTypeNumbers_IgnoresRemovedLinesAndOtherPostageTypes()
    {
        var courier = Scheme(CourierPostageId, combinedPackaging: false, MondayId, 1);
        var dryIce = Scheme(DryIcePostageId, combinedPackaging: false, MondayId, 1);
        var realSchemes = new Dictionary<Guid, CoreScheme> { [courier.SchemeId] = courier, [dryIce.SchemeId] = dryIce };
        var schemes = new[]
        {
            PendingScheme(courier.SchemeId, 0m, false, 4),
            PendingScheme(courier.SchemeId, 0m, true, 5, 6),
            PendingScheme(dryIce.SchemeId, 0m, false, 7, 8)
        };

        Assert.Equal(1, PendingOrderPricing.PostageTypeNumbers(schemes, realSchemes, CourierPostageId));
    }

    [Theory]
    [InlineData("UK", 30)]
    [InlineData("EU", 60)]
    [InlineData("NonEU", 90)]
    public void SchemePostagePrice_ChargesPlanPricePerSelectedMonthForCountryType(string countryType, decimal expected)
    {
        var scheme = Scheme(CourierPostageId, combinedPackaging: false, MondayId, 1);
        var pending = PendingScheme(scheme.SchemeId, 0m, false, 4, 5, 6);

        var price = PendingOrderPricing.SchemePostagePrice(pending, scheme, [Plan("Courier", CourierPostageId)], countryType);

        Assert.Equal(expected, price);
    }

    [Fact]
    public void SchemePostagePrice_UnknownSchemeOrPlan_ReturnsZero()
    {
        var pending = PendingScheme(Guid.NewGuid(), 0m, false, 4);

        Assert.Equal(0m, PendingOrderPricing.SchemePostagePrice(pending, null, [Plan("Courier", CourierPostageId)], "UK"));
        Assert.Equal(0m, PendingOrderPricing.SchemePostagePrice(pending, Scheme(Guid.NewGuid(), false, MondayId, 1), [Plan("Courier", CourierPostageId)], "UK"));
    }

    [Fact]
    public void TotalPostagePrice_SumsEachNamedPlanAtItsUnitPriceTimesDespatchCount()
    {
        var courierScheme = Scheme(CourierPostageId, combinedPackaging: false, MondayId, 1);
        var dryIceScheme = Scheme(DryIcePostageId, combinedPackaging: false, MondayId, 1);
        var realSchemes = new Dictionary<Guid, CoreScheme> { [courierScheme.SchemeId] = courierScheme, [dryIceScheme.SchemeId] = dryIceScheme };
        var schemes = new[]
        {
            PendingScheme(courierScheme.SchemeId, 0m, false, 4, 5),
            PendingScheme(dryIceScheme.SchemeId, 0m, false, 6)
        };
        PostagePricingPlanEntity[] plans = [Plan("Courier", CourierPostageId), Plan("Dry Ice", DryIcePostageId)];

        // Courier: 10 x 2 despatches. Dry Ice: 10 x 1. Biofreeze plan absent, contributes nothing.
        Assert.Equal(30m, PendingOrderPricing.TotalPostagePrice(schemes, realSchemes, plans, "UK"));
    }

    [Fact]
    public void CountryTypeFor_UnknownCountry_ReturnsEmpty()
    {
        var countryId = Guid.NewGuid();
        List<CountryEntity> countries = [new() { CountryId = countryId, Country = "United Kingdom", CountryType = "UK" }];

        Assert.Equal("UK", PendingOrderPricing.CountryTypeFor(countries, countryId));
        Assert.Equal(string.Empty, PendingOrderPricing.CountryTypeFor(countries, Guid.NewGuid()));
    }
}
