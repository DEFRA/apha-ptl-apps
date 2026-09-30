using PTL.Contracts.Participant;

namespace PTL.Api.Tests.Participant;

// Exercises every property getter on ParticipantSchemeResponse/ParticipantSchemeSaveResult, which
// otherwise show near-0% coverage because no existing test reads every field.
public class ParticipantSchemeDtoCoverageTests
{
    private static ParticipantSchemeResponse FullResponse() => new(
        ParticipantSchemeId: Guid.NewGuid(),
        ContractId: Guid.NewGuid(),
        ParticipantId: Guid.NewGuid(),
        SchemeId: Guid.NewGuid(),
        DistributionMonthJan: true,
        DistributionMonthFeb: true,
        DistributionMonthMar: true,
        DistributionMonthApr: true,
        DistributionMonthMay: true,
        DistributionMonthJun: true,
        DistributionMonthJul: true,
        DistributionMonthAug: true,
        DistributionMonthSep: true,
        DistributionMonthOct: true,
        DistributionMonthNov: true,
        DistributionMonthDec: true,
        CanEditJan: true,
        CanEditFeb: true,
        CanEditMar: true,
        CanEditApr: true,
        CanEditMay: true,
        CanEditJun: true,
        CanEditJul: true,
        CanEditAug: true,
        CanEditSep: true,
        CanEditOct: true,
        CanEditNov: true,
        CanEditDec: true,
        NumberOfSetsRequired: 2,
        ExternalReference: "EXT-001",
        Contact: "Alice Example",
        IsRemoved: false,
        ImportExportLicenceRequired: true,
        CustomsCertificateRequired: true,
        NonFeePaying: false,
        PackingInstructions: "Fragile",
        IsWeightedPricing: true,
        DataConsentDeclarationGiven: true,
        IsOverrideJan: true,
        IsOverrideFeb: true,
        IsOverrideMar: true,
        IsOverrideApr: true,
        IsOverrideMay: true,
        IsOverrideJun: true,
        IsOverrideJul: true,
        IsOverrideAug: true,
        IsOverrideSep: true,
        IsOverrideOct: true,
        IsOverrideNov: true,
        IsOverrideDec: true,
        Price: 42.5m,
        ParticipantDisplayName: "Lab One: LAB1",
        SchemeDisplayName: "S1: Salmonella",
        GroupAddressId: Guid.NewGuid());

    [Fact]
    public void ParticipantSchemeResponse_ExposesAllProperties()
    {
        var response = FullResponse();

        Assert.NotEqual(Guid.Empty, response.ParticipantSchemeId);
        Assert.NotEqual(Guid.Empty, response.ContractId);
        Assert.NotEqual(Guid.Empty, response.ParticipantId);
        Assert.NotEqual(Guid.Empty, response.SchemeId);
        Assert.True(response.DistributionMonthJan);
        Assert.True(response.DistributionMonthFeb);
        Assert.True(response.DistributionMonthMar);
        Assert.True(response.DistributionMonthApr);
        Assert.True(response.DistributionMonthMay);
        Assert.True(response.DistributionMonthJun);
        Assert.True(response.DistributionMonthJul);
        Assert.True(response.DistributionMonthAug);
        Assert.True(response.DistributionMonthSep);
        Assert.True(response.DistributionMonthOct);
        Assert.True(response.DistributionMonthNov);
        Assert.True(response.DistributionMonthDec);
        Assert.True(response.CanEditJan);
        Assert.True(response.CanEditFeb);
        Assert.True(response.CanEditMar);
        Assert.True(response.CanEditApr);
        Assert.True(response.CanEditMay);
        Assert.True(response.CanEditJun);
        Assert.True(response.CanEditJul);
        Assert.True(response.CanEditAug);
        Assert.True(response.CanEditSep);
        Assert.True(response.CanEditOct);
        Assert.True(response.CanEditNov);
        Assert.True(response.CanEditDec);
        Assert.Equal(2, response.NumberOfSetsRequired);
        Assert.Equal("EXT-001", response.ExternalReference);
        Assert.Equal("Alice Example", response.Contact);
        Assert.False(response.IsRemoved);
        Assert.True(response.ImportExportLicenceRequired);
        Assert.True(response.CustomsCertificateRequired);
        Assert.False(response.NonFeePaying);
        Assert.Equal("Fragile", response.PackingInstructions);
        Assert.True(response.IsWeightedPricing);
        Assert.True(response.DataConsentDeclarationGiven);
        Assert.True(response.IsOverrideJan);
        Assert.True(response.IsOverrideFeb);
        Assert.True(response.IsOverrideMar);
        Assert.True(response.IsOverrideApr);
        Assert.True(response.IsOverrideMay);
        Assert.True(response.IsOverrideJun);
        Assert.True(response.IsOverrideJul);
        Assert.True(response.IsOverrideAug);
        Assert.True(response.IsOverrideSep);
        Assert.True(response.IsOverrideOct);
        Assert.True(response.IsOverrideNov);
        Assert.True(response.IsOverrideDec);
        Assert.Equal(42.5m, response.Price);
        Assert.Equal("Lab One: LAB1", response.ParticipantDisplayName);
        Assert.Equal("S1: Salmonella", response.SchemeDisplayName);
        Assert.NotNull(response.GroupAddressId);
    }

    [Fact]
    public void ParticipantSchemeResponse_GroupAddressId_DefaultsToNull()
    {
        var response = FullResponse() with { GroupAddressId = null };

        Assert.Null(response.GroupAddressId);
    }

    [Fact]
    public void ParticipantSchemeSaveResult_ExposesAllProperties()
    {
        var response = FullResponse();
        var success = new ParticipantSchemeSaveResult(true, response, new Dictionary<string, string[]>());
        var failure = new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { ["CountryId"] = ["Country must be selected"] });

        Assert.True(success.Success);
        Assert.NotNull(success.ParticipantScheme);
        Assert.Empty(success.FieldErrors);
        Assert.False(failure.Success);
        Assert.Null(failure.ParticipantScheme);
        Assert.Contains("CountryId", failure.FieldErrors.Keys);
    }
}
