using PTL.Contracts.Contract;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.Contract;

// Exercises every property getter on record/DTO types that otherwise show 0% coverage purely
// because nothing reads every field - see repo memory "property-getter gaps" pattern.
public class ContractDtoCoverageTests
{
    [Fact]
    public void ContractItemResponse_ExposesAllProperties()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var item = new ContractItemResponse(participantSchemeId, participantId, "LAB1", "Lab One", "Lab One: LAB1", 4, 42.5m, false, true);

        Assert.Equal(participantSchemeId, item.ParticipantSchemeId);
        Assert.Equal(participantId, item.ParticipantId);
        Assert.Equal("LAB1", item.LabCode);
        Assert.Equal("Lab One", item.LabName);
        Assert.Equal("Lab One: LAB1", item.FullName);
        Assert.Equal(4, item.NumberOfDistributions);
        Assert.Equal(42.5m, item.Price);
        Assert.False(item.NonFeePaying);
        Assert.True(item.HasOverride);
    }

    [Fact]
    public void ContractItemSchemeResponse_ExposesAllProperties()
    {
        var schemeId = Guid.NewGuid();
        var item = new ContractItemResponse(Guid.NewGuid(), Guid.NewGuid(), "LAB1", "Lab One", "Lab One: LAB1", 4, 42.5m, false, false);
        var scheme = new ContractItemSchemeResponse(schemeId, "S1", "Salmonella", [item]);

        Assert.Equal(schemeId, scheme.SchemeId);
        Assert.Equal("S1", scheme.SchemeIdentifier);
        Assert.Equal("Salmonella", scheme.SchemeName);
        Assert.Single(scheme.Participants);
    }

    [Fact]
    public void ContractItemsResponse_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var response = new ContractItemsResponse(
            contractId, "A", 2026, "QAL/00001", "\u00a3", 0.1m, 25m,
            2, 5m, 10m, 3, 4m, 12m, 1, 6m, 6m, 9.5m, 85m, 95.5m, false, []);

        Assert.Equal(contractId, response.ContractId);
        Assert.Equal("A", response.Suffix);
        Assert.Equal(2026, response.YearId);
        Assert.Equal("QAL/00001", response.QalNumber);
        Assert.Equal("\u00a3", response.Symbol);
        Assert.Equal(0.1m, response.DiscountRate);
        Assert.Equal(25m, response.AdministrationCharge);
        Assert.Equal(2, response.NumberCourier);
        Assert.Equal(5m, response.CourierPrice);
        Assert.Equal(10m, response.CourierPriceTotal);
        Assert.Equal(3, response.NumberPostage);
        Assert.Equal(4m, response.PostagePrice);
        Assert.Equal(12m, response.PostagePriceTotal);
        Assert.Equal(1, response.NumberSpecialDelivery);
        Assert.Equal(6m, response.SpecialDeliveryPrice);
        Assert.Equal(6m, response.SpecialDeliveryPriceTotal);
        Assert.Equal(9.5m, response.DiscountPrice);
        Assert.Equal(85m, response.TotalPriceItems);
        Assert.Equal(95.5m, response.TotalPrice);
        Assert.False(response.IsReadOnly);
        Assert.Empty(response.Schemes);
    }

    [Fact]
    public void ContractItemRemovalResult_ExposesAllProperties()
    {
        var success = new ContractItemRemovalResult(true, false, null);
        var notFound = new ContractItemRemovalResult(false, true, "not found");

        Assert.True(success.Success);
        Assert.False(success.NotFound);
        Assert.Null(success.ErrorMessage);
        Assert.False(notFound.Success);
        Assert.True(notFound.NotFound);
        Assert.Equal("not found", notFound.ErrorMessage);
    }

    [Fact]
    public void ContractRenewalResponse_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var start = new DateTime(2026, 4, 1);
        var end = new DateTime(2027, 3, 31);
        var response = new ContractRenewalResponse(
            contractId, customerId, "QAL/00001", "Sample Labs Ltd", "Alice Example",
            "1 Street", "Town", "County", "Country", "Postcode", "United Kingdom", start, end, "Renewal info");

        Assert.Equal(contractId, response.ContractId);
        Assert.Equal(customerId, response.CustomerId);
        Assert.Equal("QAL/00001", response.QalNumber);
        Assert.Equal("Sample Labs Ltd", response.OrganisationName);
        Assert.Equal("Alice Example", response.ContactName);
        Assert.Equal("1 Street", response.Address1);
        Assert.Equal("Town", response.Address2);
        Assert.Equal("County", response.Address3);
        Assert.Equal("Country", response.Address4);
        Assert.Equal("Postcode", response.Address5);
        Assert.Equal("United Kingdom", response.Country);
        Assert.Equal(start, response.ContractStartDate);
        Assert.Equal(end, response.ContractEndDate);
        Assert.Equal("Renewal info", response.RenewalInformation);
    }

    [Fact]
    public void SampleAddressResponse_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var scheme = new SampleAddressSchemeResponse(Guid.NewGuid(), "Salmonella", "S1", "Jan,Feb", 12);
        var response = new SampleAddressResponse(
            contractId, participantId, "QAL/00001", "LAB1", "Alice Example", "Lab One Ltd",
            "1 Street", "Town", "County", "Country", "Postcode", "United Kingdom",
            "020 1234 5678", "020 1234 5679", "alice@example.com", "GB123456789",
            "ACC001", "Standard", "PO12345", [scheme], []);

        Assert.Equal(contractId, response.ContractId);
        Assert.Equal(participantId, response.ParticipantId);
        Assert.Equal("QAL/00001", response.QalNumber);
        Assert.Equal("LAB1", response.LabCode);
        Assert.Equal("Alice Example", response.ContactName);
        Assert.Equal("Lab One Ltd", response.Organisation);
        Assert.Equal("1 Street", response.Address1);
        Assert.Equal("Town", response.Address2);
        Assert.Equal("County", response.Address3);
        Assert.Equal("Country", response.Address4);
        Assert.Equal("Postcode", response.Address5);
        Assert.Equal("United Kingdom", response.Country);
        Assert.Equal("020 1234 5678", response.Telephone);
        Assert.Equal("020 1234 5679", response.Fax);
        Assert.Equal("alice@example.com", response.Email);
        Assert.Equal("GB123456789", response.VatNumber);
        Assert.Equal("ACC001", response.AccountNumber);
        Assert.Equal("Standard", response.VatRating);
        Assert.Equal("PO12345", response.PurchaseOrderNumber);
        Assert.Single(response.FeePayingSchemes);
        Assert.Empty(response.NonFeePayingSchemes);
    }

    [Fact]
    public void SampleAddressSchemeResponse_ExposesAllProperties()
    {
        var participantSchemeId = Guid.NewGuid();
        var scheme = new SampleAddressSchemeResponse(participantSchemeId, "Salmonella", "S1", "Jan,Feb", 12);

        Assert.Equal(participantSchemeId, scheme.ParticipantSchemeId);
        Assert.Equal("Salmonella", scheme.SchemeName);
        Assert.Equal("S1", scheme.SchemeIdentifier);
        Assert.Equal("Jan,Feb", scheme.MonthsActive);
        Assert.Equal(12, scheme.WeekNumber);
    }

    [Fact]
    public void ImportPermitResponse_ExposesAllProperties()
    {
        var participantSchemeId = Guid.NewGuid();
        var expiry = new DateTime(2026, 12, 31);
        var response = new ImportPermitResponse(participantSchemeId, "PT0001", "Salmonella", "1476", true, true, expiry);

        Assert.Equal(participantSchemeId, response.ParticipantSchemeId);
        Assert.Equal("PT0001", response.SchemeNumber);
        Assert.Equal("Salmonella", response.SchemeName);
        Assert.Equal("1476", response.LabId);
        Assert.True(response.ImportPermitRequired);
        Assert.True(response.ImportPermitReceived);
        Assert.Equal(expiry, response.ImportPermitExpiry);
    }

    [Fact]
    public void RenewableContractDto_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var dto = new RenewableContractDto(contractId, "A", "Alice Example", "Renewal info", "Actions", true, 3);

        Assert.Equal(contractId, dto.ContractId);
        Assert.Equal("A", dto.Suffix);
        Assert.Equal("Alice Example", dto.ContractSignatory);
        Assert.Equal("Renewal info", dto.RenewalInformation);
        Assert.Equal("Actions", dto.ActionsRequired);
        Assert.True(dto.IsActive);
        Assert.Equal(3, dto.NoOfItems);
    }

    [Fact]
    public void RenewableContractItemDto_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var dto = new RenewableContractItemDto(contractId, "A", participantSchemeId, "LAB1", "Lab One", "S1", "Old Scheme", "S2", "New Scheme", true, "S1");

        Assert.Equal(contractId, dto.ContractId);
        Assert.Equal("A", dto.Suffix);
        Assert.Equal(participantSchemeId, dto.ParticipantSchemeId);
        Assert.Equal("LAB1", dto.LabCode);
        Assert.Equal("Lab One", dto.LabName);
        Assert.Equal("S1", dto.OldSchemeIdentifier);
        Assert.Equal("Old Scheme", dto.OldSchemeName);
        Assert.Equal("S2", dto.NewSchemeIdentifier);
        Assert.Equal("New Scheme", dto.NewSchemeName);
        Assert.True(dto.IsRenewable);
        Assert.Equal("S1", dto.Identifier);
    }

    [Fact]
    public void RenewableContractsResponse_ExposesAllProperties()
    {
        var contract = new RenewableContractDto(Guid.NewGuid(), "A", "Alice Example", "Renewal", "Actions", true, 1);
        var response = new RenewableContractsResponse(true, "blocked reason", ["Alice Example"], [contract]);

        Assert.True(response.IsAllowed);
        Assert.Equal("blocked reason", response.BlockedReason);
        Assert.Single(response.ExistingSignatories);
        Assert.Single(response.Contracts);
    }

    [Fact]
    public void RenewableContractItemsResponse_ExposesAllProperties()
    {
        var item = new RenewableContractItemDto(Guid.NewGuid(), "A", Guid.NewGuid(), "LAB1", "Lab One", "S1", "Old", "S2", "New", true, "S1");
        var response = new RenewableContractItemsResponse([item]);

        Assert.Single(response.Items);
    }

    [Fact]
    public void RenewContractRequest_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var request = new RenewContractRequest([contractId], [participantSchemeId], "Alice Example");

        Assert.Single(request.ContractIds);
        Assert.Single(request.ParticipantSchemeIds);
        Assert.Equal("Alice Example", request.NewContractSignatory);
    }

    [Fact]
    public void RenewContractResponse_ExposesAllProperties()
    {
        var newContractId = Guid.NewGuid();
        var success = new RenewContractResponse(true, newContractId, null);
        var failure = new RenewContractResponse(false, null, "error");

        Assert.True(success.Success);
        Assert.Equal(newContractId, success.NewContractId);
        Assert.Null(success.ErrorMessage);
        Assert.False(failure.Success);
        Assert.Null(failure.NewContractId);
        Assert.Equal("error", failure.ErrorMessage);
    }

    [Fact]
    public void ContractRenewalEntity_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var start = new DateTime(2026, 4, 1);
        var end = new DateTime(2027, 3, 31);
        var entity = new ContractRenewalEntity
        {
            ContractId = contractId,
            CustomerId = customerId,
            QalNumber = "QAL/00001",
            OrganisationName = "Sample Labs Ltd",
            ContactName = "Alice Example",
            Address1 = "1 Street",
            Address2 = "Town",
            Address3 = "County",
            Address4 = "Country",
            Address5 = "Postcode",
            Country = "United Kingdom",
            ContractStartDate = start,
            ContractEndDate = end,
            RenewalInformation = "Renewal info"
        };

        Assert.Equal(contractId, entity.ContractId);
        Assert.Equal(customerId, entity.CustomerId);
        Assert.Equal("QAL/00001", entity.QalNumber);
        Assert.Equal("Sample Labs Ltd", entity.OrganisationName);
        Assert.Equal("Alice Example", entity.ContactName);
        Assert.Equal("1 Street", entity.Address1);
        Assert.Equal("Town", entity.Address2);
        Assert.Equal("County", entity.Address3);
        Assert.Equal("Country", entity.Address4);
        Assert.Equal("Postcode", entity.Address5);
        Assert.Equal("United Kingdom", entity.Country);
        Assert.Equal(start, entity.ContractStartDate);
        Assert.Equal(end, entity.ContractEndDate);
        Assert.Equal("Renewal info", entity.RenewalInformation);
    }

    [Fact]
    public void SampleAddressEntity_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var entity = new SampleAddressEntity
        {
            ContractId = contractId,
            ParticipantId = participantId,
            QalNumber = "QAL/00001",
            LabCode = "LAB1",
            ContactName = "Alice Example",
            Organisation = "Lab One Ltd",
            Address1 = "1 Street",
            Address2 = "Town",
            Address3 = "County",
            Address4 = "Country",
            Address5 = "Postcode",
            Country = "United Kingdom",
            Telephone = "020 1234 5678",
            Fax = "020 1234 5679",
            Email = "alice@example.com",
            VatNumber = "GB123456789",
            AccountNumber = "ACC001",
            VatRating = "Standard",
            PurchaseOrderNumber = "PO12345"
        };

        Assert.Equal(contractId, entity.ContractId);
        Assert.Equal(participantId, entity.ParticipantId);
        Assert.Equal("QAL/00001", entity.QalNumber);
        Assert.Equal("LAB1", entity.LabCode);
        Assert.Equal("Alice Example", entity.ContactName);
        Assert.Equal("Lab One Ltd", entity.Organisation);
        Assert.Equal("1 Street", entity.Address1);
        Assert.Equal("Town", entity.Address2);
        Assert.Equal("County", entity.Address3);
        Assert.Equal("Country", entity.Address4);
        Assert.Equal("Postcode", entity.Address5);
        Assert.Equal("United Kingdom", entity.Country);
        Assert.Equal("020 1234 5678", entity.Telephone);
        Assert.Equal("020 1234 5679", entity.Fax);
        Assert.Equal("alice@example.com", entity.Email);
        Assert.Equal("GB123456789", entity.VatNumber);
        Assert.Equal("ACC001", entity.AccountNumber);
        Assert.Equal("Standard", entity.VatRating);
        Assert.Equal("PO12345", entity.PurchaseOrderNumber);
        Assert.Empty(entity.FeePayingSchemes);
        Assert.Empty(entity.NonFeePayingSchemes);
    }

    [Fact]
    public void SampleAddressSchemeEntity_ExposesAllProperties()
    {
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var entity = new SampleAddressSchemeEntity
        {
            ContractId = contractId,
            ParticipantId = participantId,
            ParticipantSchemeId = participantSchemeId,
            SchemeName = "Salmonella",
            SchemeIdentifier = "S1",
            MonthsActive = "Jan,Feb",
            WeekNumber = 12
        };

        Assert.Equal(contractId, entity.ContractId);
        Assert.Equal(participantId, entity.ParticipantId);
        Assert.Equal(participantSchemeId, entity.ParticipantSchemeId);
        Assert.Equal("Salmonella", entity.SchemeName);
        Assert.Equal("S1", entity.SchemeIdentifier);
        Assert.Equal("Jan,Feb", entity.MonthsActive);
        Assert.Equal(12, entity.WeekNumber);
    }

    [Fact]
    public void SystemSettingsEntity_ExposesAllProperties()
    {
        var entity = new SystemSettingsEntity { UTNumber = "UT3/306", NextYearWithDelayId = 2027 };

        Assert.Equal("UT3/306", entity.UTNumber);
        Assert.Equal(2027, entity.NextYearWithDelayId);
    }
}
